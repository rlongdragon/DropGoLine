using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace DropGoLine {
    public class HistoryItem {
        public DateTime Timestamp { get; set; }
        public bool IsIncoming { get; set; }
        public string Type { get; set; } // "Text", "File", "Image"
        public string Content { get; set; } // Text content or Filename
        public string? FilePath { get; set; } // Local path if available
    }

    public class HistoryManager {
        private static HistoryManager? _instance;
        public static HistoryManager Instance => _instance ??= new HistoryManager();

        public event Action<string, HistoryItem>? OnHistoryAdded;

        // Thread sync lock
        private readonly object _lock = new object();

        // Key: PeerName, Value: List of HistoryItems
        private Dictionary<string, List<HistoryItem>> _historyStore = new Dictionary<string, List<HistoryItem>>();

        private static string HistoryPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");

        private HistoryManager() {
            Load();
        }

        public void AddRecord(string peerName, bool isIncoming, string type, string content, string? filePath = null) {
            if (string.IsNullOrEmpty(peerName)) return;

            lock (_lock) {
                if (!_historyStore.ContainsKey(peerName)) {
                    _historyStore[peerName] = new List<HistoryItem>();
                }

                var item = new HistoryItem {
                    Timestamp = DateTime.Now,
                    IsIncoming = isIncoming,
                    Type = type,
                    Content = content,
                    FilePath = filePath
                };

                // 🌟 Deduplication Logic (Safety Net)
                // If the last message from this peer is identical and within 2 seconds, skip it.
                if (_historyStore[peerName].Count > 0) {
                    var last = _historyStore[peerName][_historyStore[peerName].Count - 1];
                    TimeSpan diff = item.Timestamp - last.Timestamp;
                    if (diff.TotalSeconds < 2 && 
                        last.Content == item.Content && 
                        last.Type == item.Type && 
                        last.IsIncoming == item.IsIncoming) {
                        return; // Ignore Duplicate
                    }
                }

                _historyStore[peerName].Add(item);
                Save(); // Simple save on every add for data safety
                OnHistoryAdded?.Invoke(peerName, item);
            }
        }

        public void ClearPeerHistory(string peerName) {
            lock (_lock) {
                if (_historyStore.ContainsKey(peerName)) {
                    _historyStore[peerName].Clear();
                    Save();
                }
            }
        }

        public void ClearAllHistory() {
            lock (_lock) {
                _historyStore.Clear();
                Save();
            }
        }

        public List<HistoryItem> GetHistory(string peerName) {
            lock (_lock) {
                if (_historyStore.ContainsKey(peerName)) {
                    return _historyStore[peerName].ToList(); // Return copy
                }
                return new List<HistoryItem>();
            }
        }

        public void Load() {
            lock (_lock) {
                if (File.Exists(HistoryPath)) {
                    try {
                        string json = File.ReadAllText(HistoryPath);
                        _historyStore = JsonSerializer.Deserialize<Dictionary<string, List<HistoryItem>>>(json) ?? new Dictionary<string, List<HistoryItem>>();
                    } catch {
                        _historyStore = new Dictionary<string, List<HistoryItem>>();
                    }
                }
            }
        }

        public void Save() {
            // Note: Caller typically holds lock, but we lock here to be safe if called directly.
            // Since Monitor (lock) is re-entrant, this is fine.
            lock (_lock) {
                try {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_historyStore, options);
                    File.WriteAllText(HistoryPath, json);
                } catch { }
            }
        }
    }
}
