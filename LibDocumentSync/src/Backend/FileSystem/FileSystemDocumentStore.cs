using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DocumentSync.Backend.FileSystem {

    [DocumentStore("fs")]
    public class FileSystemDocumentStore : DocumentStore {
        FileSystemDocument mRoot;
        public string RootPath { get; private set; }

        public override IDocument Root => mRoot;

        public FileSystemDocumentStore(string root) {
            if (System.IO.Directory.Exists(root)) {
                // TOOD: If not a document store get explicit permission
                // throw new Exception("Directory exists");
            }
            else {
                Directory.CreateDirectory(root);
            }
            RootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            mRoot = (FileSystemDocument)GetByPath(RootPath);
        }

        public override Stream Open(IDocument document, System.IO.FileMode mode) {
            throw new NotImplementedException();
        }

        public override IDocument Create(string path, DocumentType type) {
            path = Path.Combine(RootPath, path);
            var tail = System.IO.Path.GetFileName(path);
            var head = System.IO.Path.GetDirectoryName(path);

            return Create(GetByPath(head), tail, type);
        }

        public override IDocument Create(IDocument parent, string name, DocumentType type) {
            FileSystemInfo fsi;
            var path = MakeAbsolute(Path.Combine(parent.FullName, name));
            Console.WriteLine("Creating {0}", path);

            switch (type) {
                case DocumentType.File:
                    var fi = new FileInfo(path);
                    fi.Create().Dispose();
                    fsi = fi;
                    break;
                case DocumentType.Directory:
                    var di = new DirectoryInfo(path);
                    di.Create();
                    fsi = di;
                    break;
                default:
                    throw new Exception("DocumentType not implemented");
            }
            return new FileSystemDocument(this, fsi);
        }

        public override void Delete(IDocument arg0) {
            if (arg0.IsFile)
                File.Delete(arg0.FullName);

            if (arg0.IsDirectory)
                Directory.Delete(arg0.FullName, true);
        }

        public override void MoveTo(IDocument src, IDocument dst) {
            throw new NotImplementedException();
        }
        public override void MoveTo(IDocument src, string name) {
            throw new NotImplementedException();
        }

        public override IDocument GetById(string id) {
            // For now, id and Path are the same.
            return GetByPath(id);
        }

        public override IDocument GetByPath(string path) {
            var absPath = MakeAbsolute(path);
            FileSystemInfo fi;
            if (System.IO.Directory.Exists(absPath))
                fi = new DirectoryInfo(absPath);
            else if (System.IO.File.Exists(absPath))
                fi = new FileInfo(absPath);
            else
                return null;

            return new FileSystemDocument(this, fi);
        }

        public override DocumentWatcher Watch() {
            return new FileSystemDocumentWatcher(this);
        }

        /*
         * TODO: Likely to break on large file counts
         */
        public override IEnumerator<IDocument> GetEnumerator() {
            foreach (var item in Directory.EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)) {
                yield return LoadDocument(item);
            }
        }

        public override IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "*", SearchOption options = SearchOption.AllDirectories) {
            throw new NotImplementedException();
        }

        private FileSystemDocument LoadDocument(string path) {
            // Cache Lookups
            if (Directory.Exists(path)) {
                return new FileSystemDocument(this, new DirectoryInfo(path));

            }
            else if (File.Exists(path)) {
                return new FileSystemDocument(this, new FileInfo(path));
            }

            throw new NotImplementedException();
        }

        /**
         * Translates Document path to Filesystem Path
         */
        internal string MakeAbsolute(string path) {
            return Path.GetFullPath(Path.Combine(RootPath, "." + Path.DirectorySeparatorChar + path));
        }
        /**
         * Translates Filesystem path to Document path
         */
        internal string MakeRelative(string path) {
            return path.Substring(RootPath.Length);
        }

        public override void Update(IDocument src, Stream stream) {
            var document = (FileSystemDocument)src;
            using (var fp = document.FileInfo.Create()) {
                stream.CopyTo(fp);
            }
        }

        public override void Update(IDocument document) {
            // Does nothing? Answer the question Should Documents have
            // throw new NotImplementedException();
            // FileInfo.LastWriteTime = document.LastWriteTime;
        }
    }
}
