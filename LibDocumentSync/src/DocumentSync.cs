using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentSync {
    public abstract class DocumentComparer : IEqualityComparer<IDocument> {
        abstract public bool Equals(IDocument x, IDocument y);

        protected bool MetadataIsEqual(IDocument x, IDocument y) {
            /*
            Console.WriteLine ("DocumentComparer:\n  {0} {1} {2}\n  {3} {4} {5}",
                x.Name, x.Size, x.ModifiedTime,
            y.Name, y.Size, y.ModifiedTime);
            */

            return x.Name == y.Name &&
                    x.Size == y.Size &&
                    x.ModifiedTime == y.ModifiedTime;
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
        IDocumentStoreFactory mDocumentStoreFactory;
        public DocumentSync(params string[] backends) {
            DocumentStores = new List<IDocumentStore>();
            mDocumentStoreFactory = new DocumentStoreFactory();

            foreach (var backend in backends) {
                DocumentStores.Add(mDocumentStoreFactory.LoadDocumentStore(backend));
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

                // One item in list

                if (item.Value.Count() == 1) {
                    toSync.Add(item.Value.First());
                }
                else {
                    var items = item.Value.Distinct(new MetadataDocumentComparer());
                    if (items.Count() > 1) {
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
                    store.CreateFile(item.FullName, item.OpenRead());
                }
            }
        }


        private void ProcessQueue(Object source, System.Timers.ElapsedEventArgs e) {
            /*
            try {
                GetDriveChanges();

                var queue = SyncQueue;
                SyncQueue = new Queue<EventArgs>();

                Console.WriteLine("Executing sync queue. {0}", queue.Count);
                foreach (var change in queue) {
                    Console.WriteLine("Change: {0}", change.GetType());

                    if (change is DriveChangedEventArgs)
                        ExecuteDriveChangedEvent((DriveChangedEventArgs)change);
                    else if (change is DriveCreatedEventArgs)
                        ExecuteDriveCreatedEvent((DriveCreatedEventArgs)change);
                    else if (change is DriveDeletedEventArgs)
                        ExecuteDriveDeletedEvent((DriveDeletedEventArgs)change);
                    else if (change is DriveRenamedEventArgs)
                        ExecuteDriveRenamedEvent((DriveRenamedEventArgs)change);

                    else if (change is FileChangedEventArgs)
                        ExecuteFileChangedEvent((FileChangedEventArgs)change);
                    else if (change is FileCreatedEventArgs)
                        ExecuteFileCreatedEvent((FileCreatedEventArgs)change);
                    else if (change is FileDeletedEventArgs)
                        ExecuteFileDeletedEvent((FileDeletedEventArgs)change);
                    else if (change is FileRenamedEventArgs)
                        ExecuteFileRenamedEvent((FileRenamedEventArgs)change);
                    else { } // throw new Exception("Unknown Event type");

                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
            */
        }
    }
}