using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentSync
{
    public class DocumentSync
    {
        IDocumentStore[] DocumentStores { get; set; }
        IDocumentStore PrimaryDocumentStore { get; set; }
        public DocumentSync(params IDocumentStore[] documents)
        {
            DocumentStores = documents;
        }

        class DocumentComparer : IEqualityComparer<IDocument>
        {
            public bool Equals(IDocument x, IDocument y)
            {
                if (x.Name == y.Name && x.Size == y.Size && x.ModifiedTime == y.ModifiedTime)
                    return true;

                if (x.Name == y.Name && x.Size == y.Size && x.Md5Checksum == y.Md5Checksum)
                    return true;

                return false;
            }

            public int GetHashCode(IDocument obj)
            {
                unchecked  // overflow is fine
                {
                    int hash = 17;
                    hash = hash * 23 + (obj.Name ?? "").GetHashCode();
                    hash = hash * 23 + obj.Size.GetHashCode();
                    //hash = hash * 23 + obj.ModifiedTime.GetHashCode();
                    return hash;
                }
            }
        }

        public void Converge()
        {
            var files = new List<IDocument>();
            var srcSync = new Dictionary<String, IDocument>();
            var dstSync = new Dictionary<String, IDocument>();

            // Full Sync, Md5 only
            // Remove files which match name, size, modified time.
            // Metadata updates for file swith name, size, md5

            // Extract name, size, modified, md5 from all stores.
            foreach (var store in DocumentStores) {
                files.AddRange(store);
            }

            var conflicts = files.Distinct(new DocumentComparer());

            foreach( var file in conflicts) {
                Console.WriteLine(file.Name);
            }

            // Subtract names to find unique files(remove from lists).  Queue as sync.
            // Remaining files are conflict.
        }


        private void ProcessQueue(Object source, System.Timers.ElapsedEventArgs e)
        {
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
