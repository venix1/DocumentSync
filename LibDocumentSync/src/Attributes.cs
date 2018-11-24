namespace DocumentSync {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class DocumentStoreAttribute : System.Attribute {
        public string Scheme { get; set; }
        public double Version { get; set; }

        public DocumentStoreAttribute(string scheme) {
            Version = 1.0;
            Scheme = scheme;
        }
    }
}