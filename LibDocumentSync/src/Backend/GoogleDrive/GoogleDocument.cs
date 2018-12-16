using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Util.Store;

using DriveFile = Google.Apis.Drive.v3.Data.File;

using System.Reflection;

namespace DocumentSync.Backend.Google {

    public class GoogleDriveDocument : IDocument {
        public IDocumentStore Owner { get; private set; }
        internal DriveFile Document { get; set; }

        internal GoogleDriveDocument(GoogleDriveDocumentStore owner, DriveFile document) {
            Owner = owner;
            Document = document;

            // FullName = owner.GetPath(this);
        }

        public string Id => Document.Id;
        public string Name => Document.Name;
        // public string FullName => Owner.GetPath(Id);
        private string mFullName;
        public string FullName {
            get {
                if (mFullName == null) {
                    mFullName = ((GoogleDriveDocumentStore)Owner).GetPath(this);
                }
                return mFullName;
            }
        }
        public long Size => Document.Size.GetValueOrDefault(0);
        public DateTime CreatedTime => Document.CreatedTime.GetValueOrDefault(DateTime.Now);
        public DateTime ModifiedTime {
            get => Document.ModifiedTime.GetValueOrDefault(DateTime.Now);
            set {
                Document.ModifiedTime = value; Owner.Update(this);
            }
        }
        public long Version => Document.Version.GetValueOrDefault();
        public bool Deleted {
            get {
                throw new System.NotImplementedException();
            }

            set {
                throw new System.NotImplementedException();
            }
        }

        public IDocument Parent {
            get {
                if (Document.Parents == null)
                    return null;

                if (Document.Parents.Count > 1)
                    throw new Exception("Unable to handle multiple Drive Parents");

                return Owner.GetById(Document.Parents[0]);
            }
        }

        public bool Trashed { get { return Document.Trashed.GetValueOrDefault(false); } }
        public bool Exists { get { return Owner.GetById(Document.Id) != null; } }
        //public bool IsDirectory { get { return Document.MimeType == Owner.DirectoryType; } }
        public bool IsDirectory => Document.MimeType == GoogleDriveDocumentStore.DirectoryType;
        public bool IsFile { get { return !IsDirectory; } }

        public string Md5Checksum { get { return Document.Md5Checksum; } }

        public System.Collections.IEnumerable Children {
            get {
                return Owner.List(this);
            }
        }

        public StreamReader OpenText() {
            throw new NotImplementedException();
        }

        public Stream OpenRead() {
            return Owner.Open(this, System.IO.FileMode.Open);
        }


        public void Delete() {
            Owner.Delete(this);
        }

        public void Update(Stream stream) {
            Owner.Update(this, stream);
        }
    }
}