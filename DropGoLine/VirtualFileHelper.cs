using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DropGoLine {
    // 實作 Virtual File Drag 的核心類別 (使用 COM 介面以支援 Stream 模式，解決 OOM 問題)
    [ComVisible(true)]
    public class VirtualFileDataObject : System.Runtime.InteropServices.ComTypes.IDataObject, IDisposable {
        private const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW";
        private const string CFSTR_FILECONTENTS = "FileContents";
        private const string CFSTR_PERFORMEDDROPEFFECT = "Performed DropEffect";
        private const string CFSTR_PREFERREDDROPEFFECT = "Preferred DropEffect";

        private static readonly short CF_FK_FILEDESCRIPTORW = (short)DataFormats.GetFormat(CFSTR_FILEDESCRIPTORW).Id;
        private static readonly short CF_FK_FILECONTENTS = (short)DataFormats.GetFormat(CFSTR_FILECONTENTS).Id;
        private static readonly short CF_FK_PERFORMEDDROPEFFECT = (short)DataFormats.GetFormat(CFSTR_PERFORMEDDROPEFFECT).Id;
        private static readonly short CF_FK_PREFERREDDROPEFFECT = (short)DataFormats.GetFormat(CFSTR_PREFERREDDROPEFFECT).Id;

        // 下載回呼：當 Explorer 要求讀取檔案內容時觸發
        private Func<Stream, Task> _downloadAction;
        private string _fileName;
        private long _fileSize;

        public VirtualFileDataObject(Func<Stream, Task> downloadAction, string fileName, long fileSize) {
            _downloadAction = downloadAction;
            _fileName = fileName;
            _fileSize = fileSize;
        }
        
        // IDisposable 實作 (雖單純 COM 物件不需要，但若有資源需釋放可放此)
        public void Dispose() {
            // No unmanaged resources directly held, but good practice
        }

        // === System.Runtime.InteropServices.ComTypes.IDataObject 實作 ===

        public void GetData(ref FORMATETC format, out STGMEDIUM medium) {
            medium = new STGMEDIUM();
            
            if (format.cfFormat == CF_FK_FILEDESCRIPTORW) {
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = GetFileDescriptor();
                medium.pUnkForRelease = null;
            } 
            else if (format.cfFormat == CF_FK_HDROP) {
                // CF_HDROP -> 回傳檔案路徑 (最快，原生速度)
                // 必須等待下載完成才能給出完整檔案
                WaitForDownloadCompletion();
                
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = GetDropFiles();
                medium.pUnkForRelease = null;
            }
            else {
                throw new COMException("Invalid Format", unchecked((int)0x80040064)); // DV_E_FORMATETC
            }
        }

        private void WaitForDownloadCompletion() {
            // 簡單等待下載任務完成
            // 注意：這會阻塞 UI 執行緒，但對於 CF_HDROP 這是必要的，
            // 因為 Windows 拿到路徑後會立刻去讀，若檔案不完整會出錯。
            // 既然下載速度快，這段等待應該是可接受的。
            if (_downloadTask != null && !_downloadTask.IsCompleted) {
                _downloadTask.Wait();
            }
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) {
            throw new COMException("Not Implemented", unchecked((int)0x80004001)); // E_NOTIMPL
        }

        public int QueryGetData(ref FORMATETC format) {
            if (format.cfFormat == CF_FK_FILEDESCRIPTORW || 
                format.cfFormat == CF_FK_HDROP || 
                format.cfFormat == CF_FK_PERFORMEDDROPEFFECT ||
                format.cfFormat == CF_FK_PREFERREDDROPEFFECT) 
            {
                return 0; // S_OK
            }
            return unchecked((int)0x80040064); 
        }

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut) {
            formatOut = formatIn;
            return unchecked((int)0x8004006D); 
        }

        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release) {
             throw new COMException("Not Implemented", unchecked((int)0x80004001));
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

        // ... DAdvise, etc ...

        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection) {
             connection = 0; return unchecked((int)0x80040003); 
        }
        public void DUnadvise(int connection) { throw new COMException("Not Implemented", unchecked((int)0x80040003)); }
        public int EnumDAdvise(out IEnumSTATDATA enumAdvise) { enumAdvise = null!; return unchecked((int)0x80040003); }

        // === 內部邏輯 helpers ===

        private IntPtr GetFileDescriptor() {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write((uint)1); // cItems
            
            // FILEDESCRIPTORW
            uint flags = 0x0040; // FD_FILESIZE
            bw.Write(flags); 
            bw.Write(new byte[16]); // clsid
            bw.Write(0); bw.Write(0); // sizel
            bw.Write(0); bw.Write(0); // pointl
            bw.Write((uint)0x80); // dwFileAttributes (Normal)

            bw.Write((long)0); bw.Write((long)0); bw.Write((long)0); // times
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

        // 產生 DROPFILES 結構讓 Explorer 讀取實體檔案
        private IntPtr GetDropFiles() {
            // DROPFILES structure
            // DWORD pFiles; // Offset to file list
            // POINT pt;
            // BOOL fNC;
            // BOOL fWide;
            
            // 結構大小: 20 bytes
            int offset = 20;
            
            // 檔案路徑清單 (Double null terminated)
            // _tempFilePath + \0 + \0
            byte[] pathBytes = Encoding.Unicode.GetBytes(_tempFilePath); 
            int size = offset + pathBytes.Length + 2 + 2; // + null char + final null char
            
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            // 寫入 Header
            Marshal.WriteInt32(ptr, 0, offset); // pFiles
            Marshal.WriteInt32(ptr, 4, 0); // pt.x
            Marshal.WriteInt32(ptr, 8, 0); // pt.y
            Marshal.WriteInt32(ptr, 12, 0); // fNC
            Marshal.WriteInt32(ptr, 16, 1); // fWide (Unicode)
            
            // 寫入路徑
            Marshal.Copy(pathBytes, 0, ptr + offset, pathBytes.Length);
            Marshal.WriteInt16(ptr + offset + pathBytes.Length, 0); // null
            Marshal.WriteInt16(ptr + offset + pathBytes.Length + 2, 0); // double null
            
            return ptr;
        }
    }

    // === COM Enumerator 實作 ===
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
                
            return fetched == celt ? 0 : 1; // S_OK : S_FALSE
        }

        public int Skip(int celt) {
            _current += celt;
            if (_current > _formats.Length) _current = _formats.Length;
             return 0;
        }

        public int Reset() {
            _current = 0;
            return 0; // S_OK
        }

        public void Clone(out IEnumFORMATETC newEnum) {
            var copy = new EnumFORMATETC(_formats);
            copy._current = _current;
            newEnum = copy;
        }
    }
    
    // === IStream Wrapper ===
    // 將 .NET Stream 轉換為 COM IStream
    public class IStreamWrapper : IStream {
        private Stream _baseStream;

        public IStreamWrapper(Stream stream) {
            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public void Read(byte[] pv, int cb, IntPtr pcbRead) {
            int read = _baseStream.Read(pv, 0, cb);
            if (pcbRead != IntPtr.Zero) {
                Marshal.WriteInt32(pcbRead, read);
            }
        }

        public void Write(byte[] pv, int cb, IntPtr pcbWritten) {
            _baseStream.Write(pv, 0, cb);
             if (pcbWritten != IntPtr.Zero) {
                Marshal.WriteInt32(pcbWritten, cb);
            }
        }

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition) {
             long pos = _baseStream.Seek(dlibMove, (SeekOrigin)dwOrigin);
             if (plibNewPosition != IntPtr.Zero) {
                Marshal.WriteInt64(plibNewPosition, pos);
            }
        }

        public void SetSize(long libNewSize) {
            _baseStream.SetLength(libNewSize);
        }

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten) {
            throw new NotImplementedException();
        }

        public void Commit(int grfCommitFlags) {
            _baseStream.Flush();
        }

        public void Revert() {
            throw new NotImplementedException();
        }

        public void LockRegion(long libOffset, long cb, int dwLockType) {
            throw new NotImplementedException();
        }

        public void UnlockRegion(long libOffset, long cb, int dwLockType) {
            throw new NotImplementedException();
        }

        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag) {
            pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG {
                type = 2, // STGTY_STREAM
                cbSize = _baseStream.CanSeek ? _baseStream.Length : 0, 
                grfMode = 0
            };
        }

        public void Clone(out IStream ppstm) {
            throw new NotImplementedException();
        }
    }


}
