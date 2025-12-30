using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DropGoLine {
  public class P2PManager {
    // Singleton Instance
    private static P2PManager _instance;
    public static P2PManager Instance => _instance ??= new P2PManager();

    // Constants
    private const int SERVER_PORT = 8888;
    private const int HEARTBEAT_INTERVAL = 3000;
    private const int FILE_CHUNK_SIZE = 4096; // 4KB for Base64 safety on Relay

    // State
    public string CurrentCode {
      get; private set;
    }
    private string ServerIP = "127.0.0.1";
    private TcpClient? serverClient;
    private StreamReader? serverReader;
    private StreamWriter? serverWriter;
    private TcpListener? p2pListener;
    private int localP2PPort;
    private ConcurrentDictionary<string, StreamWriter> peerWriters = new ConcurrentDictionary<string, StreamWriter>();
    private ConcurrentDictionary<string, bool> directConnectedPeers = new ConcurrentDictionary<string, bool>();

    // File Transfer State
    private TcpListener? fileServerListener;
    private int fileServerPort;
    private string? fileServerCurrentPath;

    // Events
    public event EventHandler<string>? OnIDChanged;
    public event EventHandler<P2PMessage>? OnMessageReceived; // Changed to structured message
    public event EventHandler<string>? OnPeerConnected;
    public event EventHandler<string>? OnPeerDisconnected;

    public class P2PMessage {
      public string Sender { get; set; } = "";
      public ModernCard.ContentType Type {
        get; set;
      }
      public string Content { get; set; } = ""; // Text or Filename
      public object? Tag {
        get; set;
      } // Extra data (Size, etc)
    }

    private P2PManager() {
    }

    public void Initialize(string serverIP) {
      this.ServerIP = serverIP;
      this.CurrentCode = "Loading...";
      OnIDChanged?.Invoke(this, this.CurrentCode);
      StartP2PListener();
      Task.Run(ConnectToServer);
    }

    private void StartP2PListener() {
      try {
        p2pListener = new TcpListener(IPAddress.Any, 0);
        p2pListener.Start();
        localP2PPort = ((IPEndPoint)p2pListener.LocalEndpoint).Port;
        Task.Run(AcceptP2PClients);
      } catch (Exception ex) {
        MessageBox.Show($"P2P Listener Error: {ex.Message}");
      }
    }

    private async Task AcceptP2PClients() {
      while (true) {
        try {
          TcpClient client = await p2pListener.AcceptTcpClientAsync();
          _ = Task.Run(() => HandlePeer(client));
        } catch { break; }
      }
    }

    // === File Transfer Logic (Side Channel) ===
    public int StartFileServer(string filePath) {
      try {
        if (fileServerListener == null) {
          fileServerListener = new TcpListener(IPAddress.Any, 0);
          fileServerListener.Start();
          fileServerPort = ((IPEndPoint)fileServerListener.LocalEndpoint).Port;
          Task.Run(AcceptFileClients);
        }
        fileServerCurrentPath = filePath;
        return fileServerPort;
      } catch { return -1; }
    }

    private async Task AcceptFileClients() {
      while (fileServerListener != null) {
        try {
          TcpClient client = await fileServerListener.AcceptTcpClientAsync();
          _ = Task.Run(() => ServeFile(client));
        } catch { break; }
      }
    }

    private async Task ServeFile(TcpClient client) {
      try {
        using NetworkStream ns = client.GetStream();
        if (string.IsNullOrEmpty(fileServerCurrentPath) || !File.Exists(fileServerCurrentPath))
          return;

        byte[] fileBytes = await File.ReadAllBytesAsync(fileServerCurrentPath);
        await ns.WriteAsync(fileBytes, 0, fileBytes.Length);
      } catch { } finally { client.Close(); }
    }

    public async Task DownloadFileDirect(string host, int port, string savePath) {
      try {
        using TcpClient client = new TcpClient();
        await client.ConnectAsync(host, port);
        using NetworkStream ns = client.GetStream();
        using FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);
        await ns.CopyToAsync(fs);
        MessageBox.Show($"檔案下載完成: {savePath}", "成功");
      } catch (Exception ex) {
        MessageBox.Show($"下載失敗: {ex.Message}", "錯誤");
      }
    }

    // === P2P & Server Logic ===

    private async Task ConnectToServer() {
      try {
        serverClient = new TcpClient();
        await serverClient.ConnectAsync(ServerIP, SERVER_PORT);
        var stream = serverClient.GetStream();
        serverReader = new StreamReader(stream, Encoding.UTF8);
        serverWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        string localIP = GetLocalIPAddress();
        await serverWriter.WriteLineAsync($"REGISTER|{AppSettings.Current.DeviceName}|{localIP}|{localP2PPort}");

        _ = Task.Run(ReadServerMessages);
        _ = Task.Run(SendHeartbeat);
        await serverWriter.WriteLineAsync("CREATE");

      } catch { } // Offline mode
    }

    private async Task ReadServerMessages() {
      try {
        if (serverReader == null)
          return;
        string? line;
        while ((line = await serverReader.ReadLineAsync()) != null) {
          var parts = line.Split('|');
          if (parts.Length == 0)
            continue;

          string cmd = parts[0];
          if (cmd == "MATCH" && parts.Length >= 5) {
            _ = ConnectToPeer(parts[3], int.Parse(parts[4]));
            if (parts.Length >= 6) {
                 string remoteName = parts[5];
                 OnPeerConnected?.Invoke(this, remoteName);
            }
          } else if (cmd == "RELAY" && parts.Length >= 3) {
            string sender = parts[1];
            string rawContent = string.Join("|", parts.Skip(2)); // Rejoin content in case it contains |
            HandleIncomingMessage(sender, rawContent, false);
          } else if (cmd == "CODE" && parts.Length >= 2) {
            CurrentCode = parts[1];
            OnIDChanged?.Invoke(this, CurrentCode);
          }
        }
      } catch { }
    }

    private async Task ConnectToPeer(string ip, int port) {
      try {
        TcpClient client = new TcpClient();
        await client.ConnectAsync(ip, port);
        _ = Task.Run(() => HandlePeer(client));
      } catch { }
    }

    private async Task HandlePeer(TcpClient client) {
      string remoteName = "Unknown";
      string remoteIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
      try {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await writer.WriteLineAsync($"NAME|{CurrentCode}|{AppSettings.Current.DeviceName}");

        string? line;
        while ((line = await reader.ReadLineAsync()) != null) {
          var parts = line.Split('|');
          if (parts.Length == 0)
            continue;

          if (parts[0] == "NAME" && parts.Length >= 2) {
            remoteName = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim();
            if (!string.IsNullOrEmpty(remoteName)) {
              peerWriters.TryAdd(remoteName, writer);
              directConnectedPeers.TryAdd(remoteName, true);
              OnPeerConnected?.Invoke(this, remoteName);
            }
          } else if (parts[0] == "MSG") {
            // MSG|Code|Name|Type|Content|Extra
            // For compatibility with old peers, handle length differences
            string content = "";
            if (parts.Length >= 4) { // New format or Old format?
                                     // Let's assume standard format: MSG|senderCode|senderName|...
              string senderName = parts[2];
              string msgPayload = string.Join("|", parts.Skip(3));
              HandleIncomingMessage(senderName, msgPayload, true, remoteIP);
            }
          }
        }
      } catch { } finally {
        if (remoteName != "Unknown") {
          peerWriters.TryRemove(remoteName, out _);
          directConnectedPeers.TryRemove(remoteName, out _);
          OnPeerDisconnected?.Invoke(this, remoteName);
        }
        client.Close();
      }
    }

    private void HandleIncomingMessage(string sender, string payload, bool isDirect, string fromIP = "") {
      // Payload formats:
      // TEXT|{Content}
      // FILE_OFFER|{Filename}|{Size}
      // FILE_REQ|{Filename}
      // FILE_PORT|{Port}


      var parts = payload.Split('|');
      string type = parts[0];

      // Fix: Ensure peer is visible if we receive a message via relay (and we don't have a direct connection yet)
      if (!string.IsNullOrEmpty(sender) && !directConnectedPeers.ContainsKey(sender)) {
          // Fire event on main thread via Form1 invoke check, but here we just invoke the event
          // The Form1 handles the duplicate check (ContainsKey)
          OnPeerConnected?.Invoke(this, sender);
      }


      if (type == "TEXT") {
        string text = parts.Length > 1 ? parts[1] : "";
        OnMessageReceived?.Invoke(this, new P2PMessage {
          Sender = sender,
          Type = ModernCard.ContentType.Text,
          Content = text
        });
      } else if (type == "FILE_OFFER") {
        string fname = parts.Length > 1 ? parts[1] : "Unknown";
        string size = parts.Length > 2 ? parts[2] : "0";
        OnMessageReceived?.Invoke(this, new P2PMessage {
          Sender = sender,
          Type = ModernCard.ContentType.File_Offer,
          Content = fname,
          Tag = size
        });
      } else if (type == "FILE_REQ") {
        // Sender requested file, if we are the host, start server and reply port
        // Safety check: verify we offered this file? For now just serve current.
        int port = StartFileServer(fileServerCurrentPath); // Re-use invalid path just to get port? No, need state.
                                                           // Simplification: Assume user just dragged a file, so it's in fileServerCurrentPath
        BroadcastDirect(sender, $"FILE_PORT|{port}");
      } else if (type == "FILE_PORT") {
        int port = int.Parse(parts[1]);
        // Auto download? Or trigger event?
        // Usually we trigger event to let UI start download
        OnMessageReceived?.Invoke(this, new P2PMessage {
          Sender = sender,
          Type = ModernCard.ContentType.File_Transferring,
          Content = fromIP,
          Tag = port
        });
      } else {
        // Fallback for plain text from old peers or simple relays
        OnMessageReceived?.Invoke(this, new P2PMessage {
          Sender = sender,
          Type = ModernCard.ContentType.Text,
          Content = payload
        });
      }
    }

    private async Task SendHeartbeat() {
      while (serverClient != null && serverClient.Connected) {
        try {
          await serverWriter.WriteLineAsync("PING");
          await Task.Delay(HEARTBEAT_INTERVAL);
        } catch { break; }
      }
    }

    public void Broadcast(string type, string content, string extra = "") {
      string myName = AppSettings.Current.DeviceName;
      string payload = $"{type}|{content}";
      if (!string.IsNullOrEmpty(extra))
        payload += $"|{extra}";

      // 1. Send to Direct Peers
      foreach (var kvp in peerWriters) {
        try {
          kvp.Value.WriteLine($"MSG|{CurrentCode}|{myName}|{payload}");
        } catch { }
      }

      // 2. Send via Relay (Only Text and File Offers, Binary handled separately if implementation needed)
      // If type is FILE_OFFER, we should probably encode file if we want Relay support.
      // For now, we only support Side Channel for Files.
      if (serverWriter != null && type != "FILE_REQ" && type != "FILE_PORT") {
        serverWriter.WriteLine($"RELAY|{myName}: {payload}");
      }
    }

    public void BroadcastDirect(string targetName, string payload) {
      if (peerWriters.TryGetValue(targetName, out var writer)) {
        writer.WriteLine($"MSG|{CurrentCode}|{AppSettings.Current.DeviceName}|{payload}");
      } else {
        // Fallback to Relay
        var parts = payload.Split('|');
        if (parts.Length >= 1) {
            string type = parts[0];
            string content = parts.Length > 1 ? string.Join("|", parts.Skip(1)) : "";
            Broadcast(type, content);
        }
      }
    }

    public void Join(string code) {
      if (serverWriter != null)
        serverWriter.WriteLine($"JOIN|{code}");
    }
    public void Disconnect() {
      try {
        serverClient?.Close();
      } catch { }
    }

    private string GetLocalIPAddress() {
      try {
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
          // Connect to the Server IP to see which local interface is used.
          // If ServerIP is loopback, it returns loopback. 
          // If ServerIP is external, it returns the LAN IP.
          // We use port 80 just for the route lookup (no actual packet sent).
          IPAddress target = IPAddress.Loopback; // Placeholder
          if (IPAddress.TryParse(ServerIP, out var ip))
            target = ip;
          else
            target = IPAddress.Parse("8.8.8.8"); // Fallback to Google DNS to find Internet-facing IP

          socket.Connect(target, 65530);
          IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
          return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
      } catch {
        // Fallback to old method if socket fails
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
          if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
            return ip.ToString();
        }
        return "127.0.0.1";
      }
    }
  }
}
