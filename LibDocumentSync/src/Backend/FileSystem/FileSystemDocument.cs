using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync.Backend.FileSystem {

    public class FileSystemDocument : Document {
        // Unique
        internal FileSystemInfo Document { get; set; }
        internal FileInfo FileInfo { get { return Document as FileInfo; } set { Document = value; } }
        internal DirectoryInfo DirectoryInfo { get { return Document as DirectoryInfo; } set { Document = value; } }
        internal FileSystemDocument(FileSystemDocumentStore owner, FileSystemInfo fsi) {
            Owner = owner;
            Document = fsi;
        }

        private string mMd5Sum;
        private string CalculateMd5Sum() {
            using (var md5hash = MD5.Create()) {
                if (IsFile) {
                    using (var stream = OpenRead()) {
                        var hash = md5hash.ComputeHash(stream);
                        mMd5Sum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                else {
                    mMd5Sum = "d41d8cd98f00b204e9800998ecf8427e";
                }
            }
            return mMd5Sum;
        }

        // Interface 
        public override DateTime CreationTimeUtc => Document.CreationTimeUtc;
        public override IDocument Directory => throw new NotImplementedException();

        public override DocumentType DocumentType => throw new NotImplementedException();
        public override bool Exists => FileInfo.Exists;

        public override string FullName => ((FileSystemDocumentStore)Owner).MakeRelative(Document.FullName);

        public override string Id => FullName;

        public override bool IsReadOnly { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override DateTime LastModifiedTimeUtc {
            get {
                return Document.LastWriteTimeUtc;
            }
            set {
                Document.LastWriteTimeUtc = value;
            }
        }

        public override long Length => IsFile ? FileInfo.Length : 0;

        public override string Md5sum => CalculateMd5Sum();

        public override string Name => Document.Name;

        public override bool Trashed => throw new NotImplementedException();

        public override long Version => throw new NotImplementedException();
    }
}