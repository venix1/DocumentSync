using System;

namespace DocumentSync {
    class MainClass {
        public static void Main(string[] args) {
            if (args.Length != 2) {
                Console.WriteLine("Usage: DocumentSync.exe <source> <destination>");
                return;
            }

            var program = new DocumentSync(args[0], args[1]);
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
