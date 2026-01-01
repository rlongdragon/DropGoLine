using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using System.Text;
using System.Threading.Tasks;

namespace DropGoLine {

  // 定義 IDataObjectAsyncCapability 介面
  [ComImport]
  [Guid("3D8B0590-F691-11d2-8EA9-006097DF5BD4")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IDataObjectAsyncCapability {
    void SetAsyncMode([In] int fDoOpAsync);
    void GetAsyncMode([Out] out int pfIsOpAsync);
    void StartOperation([In] IBindCtx? pbcReserved);
    void InOperation([Out] out int pfInAsyncOp);
    void EndOperation([In] int hResult, [In] IBindCtx? pbcReserved, [In] uint dwEffects);
  }

  // 支援 Async Drop 與進度視窗的 DataObject
  [ComVisible(true)]
  public class VirtualFileDataObject : System.Runtime.InteropServices.ComTypes.IDataObject, IDataObjectAsyncCapability, IDisposable {
    private const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW";
    private const string CFSTR_PERFORMEDDROPEFFECT = "Performed DropEffect";
    private const string CFSTR_PREFERREDDROPEFFECT = "Preferred DropEffect";

    private static readonly short CF_FK_FILEDESCRIPTORW = (short)DataFormats.GetFormat(CFSTR_FILEDESCRIPTORW).Id;
    private static readonly short CF_FK_PERFORMEDDROPEFFECT = (short)DataFormats.GetFormat(CFSTR_PERFORMEDDROPEFFECT).Id;
    private static readonly short CF_FK_PREFERREDDROPEFFECT = (short)DataFormats.GetFormat(CFSTR_PREFERREDDROPEFFECT).Id;
    private static readonly short CF_FK_HDROP = (short)DataFormats.GetFormat(DataFormats.FileDrop).Id;

    // 修改 Delegate 簽章以支援進度回呼
    private readonly Func<Stream, Action<float>?, Task> _downloadAction;
    private readonly string _fileName;
    private readonly long _fileSize;

    private readonly string _tempFilePath;
    private bool _isDownloaded = false;

    // Async State
    private bool _inAsyncOp = false;
    private bool _asyncMode = false;

    public VirtualFileDataObject(Func<Stream, Action<float>?, Task> downloadAction, string fileName, long fileSize) {
      _downloadAction = downloadAction;
      _fileName = fileName;
      _fileSize = fileSize;
      _tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
    }

    public void Dispose() {
    }

    // === IDataObjectAsyncCapability 實作 ===

    public void SetAsyncMode(int fDoOpAsync) {
      _asyncMode = (fDoOpAsync != 0);
    }

    public void GetAsyncMode(out int pfIsOpAsync) {
      pfIsOpAsync = _asyncMode ? 1 : 0;
    }

    public void StartOperation(IBindCtx? pbcReserved) {
      _inAsyncOp = true;

      // 這是 Explorer 在背景呼叫的，我們可以安全地在這裡執行下載，不會卡住 UI 拖曳
      // 但為了顯示進度視窗，我們需要 Invoke 回 UI thread (Application.OpenForms[0])

      Form? mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
      ProgressForm? progressForm = null;

      if (mainForm != null) {
        mainForm.Invoke(new Action(() => {
          progressForm = new ProgressForm(_fileName);
          progressForm.Show();
        }));
      }

      // 啟動下載任務 (ThreadPool)
      Task.Run(async () => {
        try {
          using (var fs = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
            // 執行下載，並傳入進度回呼
            await _downloadAction(fs, (p) => {
              if (progressForm != null && !progressForm.IsDisposed) {
                progressForm.UpdateProgress(p);
              }
            });
          }
          _isDownloaded = true;

          // 通知 Explorer 成功 (DROPIMAGE_COPY = 1)
          EndOperation(0, null, 1);
        } catch (Exception ex) {
          System.Diagnostics.Debug.WriteLine($"Async Download Failed: {ex}");
          // 通知 Explorer 失敗
          EndOperation(unchecked((int)0x80004005), null, 0); // E_FAIL
        } finally {
          // 關閉進度視窗
          if (mainForm != null && progressForm != null) {
            mainForm.Invoke(new Action(() => {
              if (!progressForm.IsDisposed)
                progressForm.Close();
            }));
          }
        }
      });
    }

    public void InOperation(out int pfInAsyncOp) {
      pfInAsyncOp = _inAsyncOp ? 1 : 0;
    }

    public void EndOperation(int hResult, IBindCtx? pbcReserved, uint dwEffects) {
      _inAsyncOp = false;
    }

    // === IDataObject 實作 ===

    public void GetData(ref FORMATETC format, out STGMEDIUM medium) {
      medium = new STGMEDIUM();

      if (format.cfFormat == CF_FK_HDROP) {
        // 如果在 Async 模式下，我們直接回傳路徑，不等待下載
        // Explorer 會在 StartOperation 後才去讀
        if (_asyncMode) {
          // Async Mode: Return path immediately
          medium.tymed = TYMED.TYMED_HGLOBAL;
          medium.unionmember = GetDropFiles();
          medium.pUnkForRelease = null;
        } else {
          // Sync Mode Fallback (For older apps): Must wait synchronously
          // 這會卡住 UI，但相容性必須
          WaitForSyncDownload();

          medium.tymed = TYMED.TYMED_HGLOBAL;
          medium.unionmember = GetDropFiles();
          medium.pUnkForRelease = null;
        }
      } else if (format.cfFormat == CF_FK_FILEDESCRIPTORW) {
        medium.tymed = TYMED.TYMED_HGLOBAL;
        medium.unionmember = GetFileDescriptor();
        medium.pUnkForRelease = null;
      } else {
        throw new COMException("Invalid Format", unchecked((int)0x80040064));
      }
    }

    private void WaitForSyncDownload() {
      if (_isDownloaded && File.Exists(_tempFilePath))
        return;
      try {
        // 同步等待下載 (注意：可能死鎖風險，但在 Sync Mode 無法避免)
        // 盡量使用 ThreadPool 避免死鎖
        Task.Run(async () => {
          using (var fs = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
            await _downloadAction(fs, null);
          }
        }).Wait();
        _isDownloaded = true;
      } catch { }
    }

    // ... Standard COM stubs ...
    public int QueryGetData(ref FORMATETC format) {
      if (format.cfFormat == CF_FK_HDROP ||
          format.cfFormat == CF_FK_FILEDESCRIPTORW ||
          format.cfFormat == CF_FK_PERFORMEDDROPEFFECT ||
          format.cfFormat == CF_FK_PREFERREDDROPEFFECT)
        return 0;
      return unchecked((int)0x80040064);
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction) {
      if (direction == DATADIR.DATADIR_GET) {
        var formats = new FORMATETC[] {
                    new FORMATETC { cfFormat = CF_FK_HDROP, ptd = IntPtr.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL },
                    new FORMATETC { cfFormat = CF_FK_FILEDESCRIPTORW, ptd = IntPtr.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL }
                };
        return new EnumFORMATETC(formats);
      }
      throw new COMException("Not Implemented", unchecked((int)0x80004001));
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Not Implemented", unchecked((int)0x80004001));
    public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut) {
      formatOut = formatIn;
      return unchecked((int)0x8004006D);
    }
    public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release) => throw new COMException("Not Implemented", unchecked((int)0x80004001));
    public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection) {
      connection = 0;
      return unchecked((int)0x80040003);
    }
    public void DUnadvise(int connection) => throw new COMException("Not Implemented", unchecked((int)0x80040003));
    public int EnumDAdvise(out IEnumSTATDATA enumAdvise) {
      enumAdvise = null!;
      return unchecked((int)0x80040003);
    }


    // === Helpers (Unchanged) ===
    private IntPtr GetDropFiles() {
      int offset = 20;
      byte[] pathBytes = Encoding.Unicode.GetBytes(_tempFilePath);
      int size = offset + pathBytes.Length + 2 + 2;
      IntPtr ptr = Marshal.AllocHGlobal(size);
      Marshal.WriteInt32(ptr, 0, offset);
      Marshal.WriteInt32(ptr, 4, 0);
      Marshal.WriteInt32(ptr, 8, 0);
      Marshal.WriteInt32(ptr, 12, 0);
      Marshal.WriteInt32(ptr, 16, 1);
      Marshal.Copy(pathBytes, 0, ptr + offset, pathBytes.Length);
      Marshal.WriteInt16(ptr + offset + pathBytes.Length, 0);
      Marshal.WriteInt16(ptr + offset + pathBytes.Length + 2, 0);
      return ptr;
    }

    private IntPtr GetFileDescriptor() {
      MemoryStream ms = new MemoryStream();
      BinaryWriter bw = new BinaryWriter(ms);
      bw.Write((uint)1);
      uint flags = 0x0040;
      bw.Write(flags);
      bw.Write(new byte[16]);
      bw.Write(0);
      bw.Write(0);
      bw.Write(0);
      bw.Write(0);
      bw.Write((uint)0x80);
      bw.Write((long)0);
      bw.Write((long)0);
      bw.Write((long)0);
      bw.Write((uint)(_fileSize >> 32));
      bw.Write((uint)(_fileSize & 0xFFFFFFFF));
      byte[] nameBytes = new byte[520];
      byte[] strBytes = Encoding.Unicode.GetBytes(_fileName);
      Array.Copy(strBytes, nameBytes, Math.Min(strBytes.Length, 520 - 2));
      bw.Write(nameBytes);
      byte[] data = ms.ToArray();
      IntPtr ptr = Marshal.AllocHGlobal(data.Length);
      Marshal.Copy(data, 0, ptr, data.Length);
      return ptr;
    }
  }

  public class EnumFORMATETC : IEnumFORMATETC {
    private FORMATETC[] _formats;
    private int _current;

    public EnumFORMATETC(FORMATETC[] formats) {
      _formats = formats;
      _current = 0;
    }

    public int Next(int celt, FORMATETC[] rgelt, int[] pceltFetched) {
      int fetched = 0;
      while (_current < _formats.Length && fetched < celt) {
        rgelt[fetched] = _formats[_current];
        _current++;
        fetched++;
      }
      if (pceltFetched != null && pceltFetched.Length > 0)
        pceltFetched[0] = fetched;
      return fetched == celt ? 0 : 1;
    }

    public int Skip(int celt) {
      _current += celt;
      if (_current > _formats.Length)
        _current = _formats.Length;
      return 0;
    }

    public int Reset() {
      _current = 0;
      return 0;
    }

    public void Clone(out IEnumFORMATETC newEnum) {
      var copy = new EnumFORMATETC(_formats);
      copy._current = _current;
      newEnum = copy;
    }
  }
}
