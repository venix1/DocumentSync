using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DocumentSync {

    public interface IDocumentStore : IEnumerable<IDocument> {
        IDocument Root { get; }
        IDocument CreateFile(string path, Stream stream);
        IDocument CreateFile(string path, string content);
        // IDocument CreateFile(IDocument parent, string name);

        /*
         * One line function bulk. 
        IDocument CreateDirectory(string path);
        IDocument CreateDirectory(IDocument parent, string name);
        */

        IDocument Create(string path, DocumentType type);
        IDocument Create(IDocument parent, string name, DocumentType type);
        Stream Open(IDocument document, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read);
        bool Exists(string path);
        void Delete(IDocument arg0);
        void MoveTo(IDocument src, IDocument dst);
        void MoveTo(IDocument src, string name);

        void Update(IDocument dst);
        void Update(IDocument dst, Stream data);
        void Copy(IDocument src, IDocument dst);
        void CopyAttributes(IDocument src, IDocument dst);
        IDocument Clone(IDocument src, string dst);

        IDocument GetById(string id);
        IDocument GetByPath(string path);
        IDocument TryGetByPath(string path);

        IEnumerable<IDocument> EnumerateFiles(string path = "/", string filter = "*", SearchOption options = SearchOption.AllDirectories);
        IEnumerable<IDocument> EnumerateFiles(IDocument path, string filter = "*", SearchOption options = SearchOption.AllDirectories);
        IEnumerable<IDocument> List(IDocument document = null);

        DocumentWatcher Watch();
    }
}