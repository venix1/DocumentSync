using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DocumentSync {

    interface IDocumentStoreFactory {
        IDocumentStore LoadDocumentStore(string uri);
    }

    public class DocumentStoreFactory : IDocumentStoreFactory {
        private static Dictionary<string, Type> DocumentStores = new Dictionary<string, Type>();

        static DocumentStoreFactory() {
            var backends = from type in Assembly.GetExecutingAssembly().GetTypes()
                           where typeof(IDocumentStore).IsAssignableFrom(type)
                           where !type.IsAbstract && !type.IsInterface
                           let attributes = type.GetCustomAttributes(
                              typeof(DocumentStoreAttribute), false)
                           where attributes != null && attributes.Length > 0
                           let attribute = attributes[0] as DocumentStoreAttribute
                           select new { type, attribute.Scheme };

            DocumentStores = backends.ToDictionary(p => p.Scheme, p => p.type);
        }

        public IDocumentStore LoadDocumentStore(string uri) {
            var options = new Uri(uri);

            var documentStoreType = DocumentStores[options.Scheme];

            if (options.Host != "")
                throw new NotImplementedException();

            return Activator.CreateInstance(documentStoreType, options.AbsolutePath) as IDocumentStore;
        }
    }
}