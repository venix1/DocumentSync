* Folder Hiearchy
* Map Drive to Disk
* Sync Queue

1. Create UnifiedFile for all Drive files
2. Create UnifiedFile for all Disk files
? File storage
  * Dump structure to disk
  ? Map path to UnifiedFile
? File indexing
  ? path/id/inode lookups
3. Iterate over all UnifiedFiles to synchronize
4. Watch for disk changes
5. Watch for drive changes.

== UnifiedFile ==
string Path: Full path relative to monitor folder
string FileMd5Sum: MD5 of local file.
GoogleDriveFile File; // Copy of Google Drive metdata
FileSystemInfo FileSystemInfo; // Base Metadata
FileInfo FileInfo; // File specific Metadata
DirectoryInfo DirectoryInfo;  // Directory Specific metadata
UnifiedState State; // State of synchornization

* State tracking.
  * States determine what condition the file.
  * Processor uses a switch on the state, to determine how to handle.
  * Isolates all sync logic to single method
  * Code only needs to determine state.
  * States as Flags 
    * Synchronized: 0 - No changes
    * Remote: Apply operation to Remote
    * Local: Apply operation to Local
    * Dirty: One of the files was updated.
    * Rename: File has been renamed.
    * Change: File was updated.
    * Delete: File was deleted
    * New: This is a new file
    Ex: Remote | New


* UnifiedFile must be serializable
* UnifiedFIle manages Cache lookups
? SQLite/LINQ backend.  On disk copy, and indexed lookups.

== Startup Synchornization ==
 * Load on disk backup in Startup queue
 * Mark all local files which have changed.
 * Read all changes from Drive
 * Conflict resolution
 * Download new drive files
 * Upload new local files.


== Conflicts ==
 * Conflict occurs if local MD5 and Drive MD5 are not equal and file is not in sync.
 ? Possible revision scan, to see if an older copy matched.
 * Older file is renamed, and newer file is sycnhronized.ive copy.