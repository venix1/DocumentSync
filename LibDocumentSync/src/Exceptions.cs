using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DocumentSync {
    public class DocumentException : SystemException {
        public DocumentException(string msg) : base(msg) { }
    }

    public class DocumentDoesNotExistException : DocumentException {
        public DocumentDoesNotExistException(string msg) : base(msg) { }
    }
}