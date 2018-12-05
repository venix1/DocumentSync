using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync.Backend.FileSystem {

    public class FileSystemDocument : IDocument {
        public IDocumentStore Owner { get; private set; }
        // FileSystemDocumentStore Owner { get; set; }
        FileSystemInfo Document { get; set; }

        internal FileInfo FileInfo { get { return Document as FileInfo; } set { Document = value; } }
        internal DirectoryInfo DirectoryInfo { get { return Document as DirectoryInfo; } set { Document = value; } }

        internal FileSystemDocument(FileSystemDocumentStore owner, FileSystemInfo fsi) {
            Owner = owner;
            Document = fsi;
        }

        // IDocument Interface

        public string Id { get => Document.FullName; }
        public string Name { get => Document.Name; }
        public string FullName { get => ((FileSystemDocumentStore)Owner).MakeRelative(Document.FullName); }
        public long Size { get => IsFile ? FileInfo.Length : 0; }

        public DateTime CreatedTime => Document.CreationTime;
        public DateTime ModifiedTime {
            get => Document.LastWriteTime;
            set => UpdateLastWriteTime(value);
        }
        public IDocument Parent => throw new NotImplementedException();
        public long Version => throw new NotImplementedException();
        public bool Deleted => throw new NotImplementedException();
        public bool Exists => throw new NotImplementedException();
        public bool Trashed => throw new NotImplementedException();

        public StreamReader OpenText() {
            return FileInfo.OpenText();
        }

        public Stream OpenRead() {
            return FileInfo.OpenRead();
        }

        public bool IsDirectory {
            get {
                return DirectoryInfo != null;
            }
        }

        public bool IsFile {
            get {
                return FileInfo != null;
            }
        }

        public System.Collections.IEnumerable Children {
            get { throw new Exception("stub"); }
        }

        public string Md5Checksum {
            get { return CalculateMd5Sum(); }
        }

        private string _Md5Checksum;
        private string CalculateMd5Sum() {
            using (var md5hash = MD5.Create()) {
                if (IsFile) {
                    using (var stream = File.OpenRead(FileInfo.FullName)) {
                        var hash = md5hash.ComputeHash(stream);
                        _Md5Checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                else {
                    _Md5Checksum = "d41d8cd98f00b204e9800998ecf8427e";
                }
            }
            return _Md5Checksum;
        }

        public void Update(System.IO.Stream stream) {
            using (var fp = FileInfo.Create()) {
                stream.CopyTo(fp);
            }
        }

        public void UpdateLastWriteTime(DateTime value) {
            FileInfo.LastWriteTime = value;
        }

        public void Delete() {
            throw new NotImplementedException("File Modifications not implemented");
        }
    }
}