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