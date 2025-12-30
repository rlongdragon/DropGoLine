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

    // Events
    public event EventHandler<string>? OnIDChanged;
    public event EventHandler<string>? OnMessageReceived;
    public event EventHandler<string>? OnPeerConnected;
    public event EventHandler<string>? OnPeerDisconnected;

    private P2PManager() {
    }

    public void Initialize(string serverIP) {
      this.ServerIP = serverIP;
      this.CurrentCode = "Loading..."; // Wait for server to assign CODE
      OnIDChanged?.Invoke(this, this.CurrentCode);

      // Start P2P Listener
      StartP2PListener();

      // Connect to Server
      Task.Run(ConnectToServer);
    }



    private void StartP2PListener() {
      try {
        p2pListener = new TcpListener(IPAddress.Any, 0);
        p2pListener.Start();
        localP2PPort = ((IPEndPoint)p2pListener.LocalEndpoint).Port;
        Task.Run(AcceptP2PClients);
      } catch (Exception ex) {
        // Simplify error handling for now
        MessageBox.Show($"P2P Listener Error: {ex.Message}");
      }
    }

    private async Task AcceptP2PClients() {
      while (true) {
        try {
          TcpClient client = await p2pListener.AcceptTcpClientAsync();
          _ = Task.Run(() => HandlePeer(client));
        } catch {
          break;
        }
      }
    }

    private async Task ConnectToServer() {
      try {
        serverClient = new TcpClient();
        await serverClient.ConnectAsync(ServerIP, SERVER_PORT);
        var stream = serverClient.GetStream();
        serverReader = new StreamReader(stream, Encoding.UTF8);
        serverWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Register
        string localIP = GetLocalIPAddress();
        await serverWriter.WriteLineAsync($"REGISTER|{CurrentCode}|{localIP}|{localP2PPort}");

        // Start tasks
        _ = Task.Run(ReadServerMessages);
        _ = Task.Run(SendHeartbeat);

        // Create Room immediately as per requirement (Auto-create own channel)
        await serverWriter.WriteLineAsync("CREATE");

      } catch (Exception) {
        // connection failed, seamless degradation to offline mode or retry logic
      }
    }

    private async Task ReadServerMessages() {
      try {
        if (serverReader == null) return;
        string? line;
        while ((line = await serverReader.ReadLineAsync()) != null) {
          var parts = line.Split('|');
          if (parts.Length == 0)
            continue;

          string cmd = parts[0];
          if (cmd == "MATCH" && parts.Length >= 5) {
            string targetPubIP = parts[1];
            int targetPubPort = int.Parse(parts[2]);
            string targetLocIP = parts[3];
            int targetLocPort = int.Parse(parts[4]);
            // Try Connect P2P
            _ = ConnectToPeer(targetLocIP, targetLocPort);
            } else if (cmd == "RELAY" && parts.Length >= 3) {
            string sender = parts[1];
            string content = parts[2];
            if (!directConnectedPeers.ContainsKey(sender)) {
              OnMessageReceived?.Invoke(this, content);
            }
          } else if (cmd == "CODE" && parts.Length >= 2) {
             // Server assigned a room code
             string newCode = parts[1];
             CurrentCode = newCode;
             OnIDChanged?.Invoke(this, CurrentCode);
          }
        }
      } catch {
        // Disconnected
      }
    }

    private async Task ConnectToPeer(string ip, int port) {
      try {
        TcpClient client = new TcpClient();
        await client.ConnectAsync(ip, port);
        _ = Task.Run(() => HandlePeer(client));
      } catch {
        // P2P connect failed
      }
    }

    private async Task HandlePeer(TcpClient client) {
      string remoteName = "Unknown";
      try {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Handshake
        await writer.WriteLineAsync($"NAME|{CurrentCode}|{AppSettings.Current.DeviceName}");

        string line;
        while ((line = await reader.ReadLineAsync()) != null) {
          var parts = line.Split('|');
          if (parts.Length == 0)
            continue;

          if (parts[0] == "NAME" && parts.Length >= 2) {
            remoteName = parts[1].Trim();
            if (parts.Length >= 3) remoteName = parts[2].Trim();
            
            if (!string.IsNullOrEmpty(remoteName)) {
                peerWriters.TryAdd(remoteName, writer);
                directConnectedPeers.TryAdd(remoteName, true);
                // Trigger event
                OnPeerConnected?.Invoke(this, remoteName);
            }
          } else if (parts[0] == "MSG" && parts.Length >= 4) {
            string senderCode = parts[1];
            string senderName = parts[2];
            string content = parts[3];
            OnMessageReceived?.Invoke(this, $"[{senderName}] {content}");
          } else if (parts[0] == "MSG" && parts.Length == 3) {
            // Fallback for old protocol
            string sender = parts[1];
            string content = parts[2];
            OnMessageReceived?.Invoke(this, $"[{sender}] {content}");
          }
        }
      } catch {
        // Peer disconnected
      } finally {
        if (remoteName != "Unknown") {
            peerWriters.TryRemove(remoteName, out _);
            directConnectedPeers.TryRemove(remoteName, out _);
            OnPeerDisconnected?.Invoke(this, remoteName);
        }
        client.Close();
      }
    }

    private async Task SendHeartbeat() {
      while (serverClient != null && serverClient.Connected) {
        try {
          await serverWriter.WriteLineAsync("PING");
          await Task.Delay(HEARTBEAT_INTERVAL);
        } catch {
          break;
        }
      }
    }

    public void Join(string code) {
      // Logic to switch room
      if (serverWriter != null) {
        serverWriter.WriteLine($"JOIN|{code}");
        // Update Current ID if needed, or just keep own ID but in different room? 
        // If the requirement is "Input code to Join", usually invalidates old room.
        // We will update CurrentCode to reflect the joined room? 
        // Actually, usually user ID != Room ID. 
        // But user specified "connection ID" which implies Channel ID.
        // Let's assume for now we join that room.
      }
    }

    public void Disconnect() {
      // Basic disconnect logic
      try {
        serverWriter?.Close();
        serverReader?.Close();
        serverClient?.Close();
        // clear peers?
      } catch { }
    }

        public void Broadcast(string content) {
            string myName = AppSettings.Current.DeviceName;
            // 1. Send to all P2P peers
            foreach (var kvp in peerWriters) {
                try {
                    // MSG|Code|DeviceName|Content
                    kvp.Value.WriteLine($"MSG|{CurrentCode}|{myName}|{content}");
                } catch { }
            }

            // 2. Send Relay to Server
            if (serverWriter != null) {
                 // RELAY|Content (Server adds name? No, server logic is fixed)
                 // Server RELAY format: RELAY|SenderID|Content
                 // We will pack name into content for RELAY fallback or accept Server Limit
                 // For now, let's keep RELAY simple content, or prefix it:
                 // "DeviceName: Content"
                 serverWriter.WriteLine($"RELAY|{myName}: {content}");
            }
        }private string GetLocalIPAddress() {
      // Simplified local IP logic
      var host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (var ip in host.AddressList) {
        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) {
          return ip.ToString();
        }
      }
      return "127.0.0.1";
    }
  }
}
