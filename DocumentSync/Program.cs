using System;

namespace DocumentSync {
    class MainClass {
        public static void Main(string[] args) {
            if (args.Length != 2) {
                Console.WriteLine("Usage: DocumentSync.exe <source> <destination>");
                return;
            }

            // TODO: Replace manual creation with factory method and arguments
            var srcStore = new FileSystemDocumentStore(args[0]);
            // var srcStore = new GoogleDriveDocumentStore(args[0]);
            var dstStore = new FileSystemDocumentStore(args[1]);
            var program = new DocumentSync(srcStore, dstStore);
            program.Convergence += (object sender, ConvergenceEventArgs e) => {
                Console.WriteLine("Converging");
                foreach (var document in e.MergeDocuments) {
                    Console.WriteLine(document.FullName);
                }

            };
            program.Converge();
        }
    }
}
