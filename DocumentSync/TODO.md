Implement Phase One using IDocumentStore Interface.
 * Implement Filesystem store.
 * Primary store treated as Master.

 * Build/Verify Document Index(Cache)
 * Persist cache across restarts.
 * Method to iterate over all data.
   * Acquire filelist for primary and secondary stores
   * Verify primary is in secondary.  Verify equal.  If not equal see if 


Database Storage.

Currently in progress of routing all GoogleDriveDocuments through Cache.
Need to monitor and 

Should GoogleDocumentStore cache metadata for files requested?
  * Cache everything could grow large. 
    * Use WeakReference for expiration?
    * Reference counting and LRU semantics?
  * If caching, should monitor drive changes for cache updates.
  * Should cached references be updated?
  * Create expired references?

  * Index required as well. Necessary for document renames and moves.
    ** Id, name, parent, version,  stored in persistent database.


Unittests.  
 * Local Create
 * Local Change
 * Local Remove
 * Local Rename
   * Rename in same folder
   * Move to another subFolder 
   * Move to parent folder
   * Move to another subfolder in parent
 * Remote Create
 * Remote Change
 * Remote Remove
 * Remove Rename

Unify GoogleDrive and FIlesystem as DocumentStore.
* Replaces UnifiedFileSystem
* Replaces UnifiedFile?
* General Events
  * DocumentChangedEventArgs
  * DocumentCreatedEventArgs
  * DocumentDeletedEventArgs
  * DocumentRenamedEventArgs
* Replaces Events wrapping FileSystemWatcher
* Replaces events wrapping Drive Logic.
* Replaces FileSystemInode


Replace FileSystemWatcher, DriveWatcher, 

Record Version numbers and md5sums for conflicts.

Null reference Drive Created Event(Not in path)

Event consolidation.
 * Check previous event, if same file, consolidate.
 * Change -> Change, Ignore
 * Change -> Delete, Remove Change
 * create -> DElete, Drop both.

Plugin hooks for events monitoring.
 * Refactor DriveUpdates into FileSystemWatcher like class.

startIndex exception on delete event. Bad Path?

NullReference with "touch test; rm test"
 * Caused by null Inode references.

Suppress Messages when event not executed.
 * Event processor needs checks for when not to execute.

Sharing violation on Drive Update. 
 * Caused by Md5Sum calculation on CalculateStatus
 * CalculateState unnecessary for EventArgs method.
 * Removed and placed into ScanDrive and ScanDisk methods.

Watcher events sometimes come out of order.
 * THIS IS NOT GOOD.  Correct order is Created, Changed, Deleted.
  File: /home/venix/projects/DocumentSync/DocumentSync/bin/Debug/PortableApps/test Changed <File must exist>
  File: /home/venix/projects/DocumentSync/DocumentSync/bin/Debug/PortableApps/test Deleted <File must exist>
  File: /home/venix/projects/DocumentSync/DocumentSync/bin/Debug/PortableApps/test Created <file must exist>
 * Solved by doing a file check

Build Queue based on Name.  
 * Created always comes first.
 * Deleted wipes out other events.

Rewrite SynchronizeFile to use change events.

Move ProcessQueue to Thread

IdMap cache used for path generation. Doesn't work with rename.
 * Wipe or rebuild cache?

 * Keep SynchronizeFile

* Extract UnifiedFIle/Inode for Event?

* Crash when uploading empty file.(Can't reproduce)
* Drive Deletes only contain FileId.  Need IdMap for removal.
  * Create Id Only File to identify deletes.
* Parent test failing.  Files randomly appearing on drive save.
  * GetDrivepath treats no parent same as Root is parent.
  * Discard no parent items?
* Solve for Delete race condition.
* Upload Excpetion if file removed before sync
* Handle multiple queued events for same object.
* Queue changes only.  Do no processing.
  * Current Locked file issue when doing MD5Sum on Local Create event.
* Update after Delete exception

* Unify CalculateStatus Modify?
* Add UnifiedFile states [DONE]
* Isolate all state changes to UnifiedFile
* Move Sync logic to single method based on state.
* Update watch code to set state.
* Lookup functions for MD5, Path, File Id.
* Conflict checking and resolution
* Split Program.cs into separate files.
* UnifiedFile Persistence.