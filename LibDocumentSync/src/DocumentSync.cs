using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentSync {
    public abstract class DocumentComparer : IEqualityComparer<IDocument> {
        abstract public bool Equals(IDocument x, IDocument y);

        protected bool MetadataIsEqual(IDocument x, IDocument y) {

            Console.WriteLine("DocumentComparer:\n  {0} {1} {2}\n  {3} {4} {5}",
                x.Name, x.Size, x.ModifiedTime,
            y.Name, y.Size, y.ModifiedTime);
            Console.WriteLine(y.ModifiedTime - x.ModifiedTime);

            Console.WriteLine((x.ModifiedTime - y.ModifiedTime).TotalSeconds);
            Console.WriteLine((x.ModifiedTime - y.ModifiedTime).TotalSeconds == 0);
            var seconds = (int)(x.ModifiedTime - y.ModifiedTime).TotalSeconds;

            return x.Name == y.Name &&
                    x.Size == y.Size &&
                    seconds == 0;
        }

        protected bool ContentIsEqual(IDocument x, IDocument y) {
            return x.Name == y.Name &&
                    x.Size == y.Size &&
                    x.Md5Checksum == y.Md5Checksum;
        }

        /* HashCode is always consistent */
        public int GetHashCode(IDocument obj) {
            //Console.WriteLine ("HashCode: {0:X} {1}", obj.FullName.GetHashCode (), obj.FullName);
            return obj.FullName.GetHashCode();
        }
    }
    public class FullDocumentComparer : DocumentComparer {
        override public bool Equals(IDocument x, IDocument y) {
            return ContentIsEqual(x, y);
        }
    }
    public class FastDocumentComparer : DocumentComparer {
        override public bool Equals(IDocument x, IDocument y) {
            return MetadataIsEqual(x, y) || ContentIsEqual(x, y);
        }
    }
    public class MetadataDocumentComparer : DocumentComparer {
        override public bool Equals(IDocument x, IDocument y) {
            Console.WriteLine("equals");
            return MetadataIsEqual(x, y);
        }
    }
    public class NameOnlyDocumentComparer : DocumentComparer {
        override public bool Equals(IDocument x, IDocument y) {
            return x.FullName == y.FullName;
        }
    }

    public class ConvergenceEventArgs : EventArgs {
        public List<IDocument> MergeDocuments;

        public ConvergenceEventArgs(List<IDocument> merge) {
            MergeDocuments = merge;
        }
    }

    public class DocumentSync {
        public EventHandler<ConvergenceEventArgs> Convergence;
        List<IDocumentStore> DocumentStores { get; set; }
        List<DocumentWatcher> Watchers { get; set; }
        IDocumentStoreFactory mDocumentStoreFactory;
        public DocumentSync(params string[] backends) {
            DocumentStores = new List<IDocumentStore>();
            Watchers = new List<DocumentWatcher>();
            mDocumentStoreFactory = new DocumentStoreFactory();

            foreach (var backend in backends) {
                var store = mDocumentStoreFactory.LoadDocumentStore(backend);
                DocumentStores.Add(store);
                var watch = store.Watch();
                Watchers.Add(watch);
                Console.WriteLine("{0}", watch);

                watch.PauseRaisingEvents = true;
                watch.EnableRaisingEvents = true;

                watch.Created += OnDocumentCreated;
                watch.Changed += OnDocumentChanged;
                watch.Deleted += OnDocumentDeleted;
                watch.Renamed += OnDocumentRenamed;
            }
        }

        public void Converge() {
            // Bootstrap watchers to catpure changes. 
            // Perform initial sync
            // Execute watchers to maintain sync
            var files = new MultiValueDictionary<string, IDocument>();
            var toSync = new List<IDocument>();

            // Extract all files from all stores.
            foreach (var store in DocumentStores) {
                foreach (var document in store) {
                    Console.WriteLine(document.FullName);
                    files.Add(document.FullName, document);
                }
            }

            foreach (var item in files) {
                Console.WriteLine("{0} {1}", item.Key, item.Value.Count());

                if (item.Value.Count() == 1) {
                    toSync.Add(item.Value.First());
                }
                else {
                    var items = item.Value.Distinct(new MetadataDocumentComparer());
                    if (items.Count() > 1) {
                        Console.WriteLine("Distinct");
                        items.Distinct(new MetadataDocumentComparer());
                        toSync.Add(items.OrderByDescending(i => i.ModifiedTime).First());
                    }
                }
            }
            Convergence?.Invoke(this, new ConvergenceEventArgs(toSync));

            foreach (var item in toSync) {
                foreach (var store in DocumentStores) {
                    if (item.Owner == store) {
                        continue;
                    }
                    Console.WriteLine("Replicating {0}:{1} to {2}", item.FullName, item.Owner, store);
                    var document = store.GetByPath(item.FullName);
                    using (var fp = item.OpenRead()) {
                        if (document == null) {
                            document = store.CreateFile(item.FullName, fp);
                        }
                        else {
                            document.Update(fp);
                        }
                    }
                    store.CopyAttributes(item, document);
                }
            }
            foreach (var watch in Watchers) {
                watch.PauseRaisingEvents = false;
            }
            Watchers[0].PollThread.Join();
        }

        private void OnDocumentCreated(object source, DocumentEventArgs e) {
            foreach (var store in DocumentStores) {
                if (e.Document.Owner == store)
                    continue;

                var dType = e.Document.IsDirectory ? DocumentType.Directory : DocumentType.File;
                var document = store.Create(e.Document.FullName, dType);
                Console.WriteLine("Document create event {0}:{1}", store, document.FullName);
                store.CopyAttributes(e.Document, document);
            }
        }
        private void OnDocumentChanged(object source, DocumentEventArgs e) {
            foreach (var store in DocumentStores) {
                if (e.Document.Owner == store)
                    continue;
                Console.WriteLine("Document change event {0}:{1}", store, e.Document.FullName);
                var document = store.GetByPath(e.Document.FullName);
                if ((new FastDocumentComparer()).Equals(e.Document, document))
                    continue;
                store.Copy(e.Document, document);
            }
        }
        private void OnDocumentRenamed(object source, DocumentEventArgs e) {
            throw new NotImplementedException("Rename not supported");
            foreach (var store in DocumentStores) {
                if (e.Document.Owner == store)
                    continue;
                Console.WriteLine("Document rename event {0}:{1}", store, e.Document.FullName);
            }
        }
        private void OnDocumentDeleted(object source, DocumentEventArgs e) {
            throw new NotImplementedException("Delete not supported");
            foreach (var store in DocumentStores) {
                if (e.Document.Owner == store)
                    continue;
                Console.WriteLine("Document delete event {0}:{1}", store, e.Document.FullName);
            }
        }
    }
}