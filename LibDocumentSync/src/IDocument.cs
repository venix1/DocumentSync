using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DocumentSync {
    public enum DocumentType {
        File,
        Directory,
        Link,
    }

    public interface IDocument {
        // Attributes
        DateTime CreationTime { get; }
        DateTime CreationTimeUtc { get; }

        IDocument Directory { get; }
        string DirectoryName { get; }
        DocumentType DocumentType { get; }
        bool Exists { get; }
        string Extension { get; }
        // ExtendedAttributes (4k limit)
        string FullName { get; }
        string Id { get; }
        bool IsDirectory { get; }
        bool IsFile { get; }

        bool IsReadOnly { get; set; }
        // DateTime LastAccessTime { get; set; }
        // DateTime LastAccessTimeUtc { get; set; }

        DateTime LastModifiedTime { get; }
        DateTime LastModifiedTimeUtc { get; }

        long Length { get; }
        string Md5sum { get; }
        string Name { get; }
        IDocumentStore Owner { get; }
        bool Trashed { get; }
        long Version { get; }

        // Creation from Document?
        // void Create(DocumentType type);
        // FileStream Create();
        // StreamWriter CreateText();

        IEnumerable<IDocument> EnumerateDocuments();
        List<IDocument> GetDocuments(string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        StreamWriter AppendText();
        IDocument CopyTo(IDocument dst, bool overwrite = false);
        // void Decrypt();
        void Delete();
        // void Encrypt();
        // GetAccessControl
        // MoveTo
        Stream Open(FileMode mode, FileAccess access, FileShare share);
        Stream OpenRead();
        StreamReader OpenText();
        Stream OpenWrite();
        void Replace(IDocument dst, string backupName, bool ignoreMetadataErrors);

        // void SetAccessControl();
    }

    public interface IDocumentEnumerable : IEnumerable<IDocument> {
    }

    public interface IDocumentEnumerator : IEnumerator<IDocument> {
    }
}
