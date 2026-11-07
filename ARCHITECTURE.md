# Architecture Overview

## System Context

This project is part of a larger system for managing Keith Long Archive data from Airtable.

### Related Projects

```
KLA/
├── AirtableToPostgres/          # Data sync: Airtable → PostgreSQL
│   ├── Syncs table data and schema
│   ├── Maintains change history
│   └── Creates typed PostgreSQL tables
│
└── AirtableImageDownloader/     # This project: Image downloads
    ├── Downloads images from specific tables
    ├── Organizes images by table and ID
    └── Maintains local image cache
```

## Shared Resources

### Airtable Base
Both projects connect to the same Airtable base:
- **Base ID**: `appnGXIULYw1ZA6bF`
- **API Key**: Shared personal access token
- **Tables**: Multiple tables including ARTWORK, ARTWORK_IMAGE, ARCHIVE_IMAGE, etc.

### PostgreSQL Database
The AirtableToPostgres project syncs to:
- **Host**: <cluster-endpoint>.rds.amazonaws.com
- **Database**: keithlongarchive

This project (AirtableImageDownloader) does NOT interact with PostgreSQL - it only downloads files.

## Data Flow

```
┌─────────────┐
│   Airtable  │
│    Base     │
└──────┬──────┘
       │
       ├─────────────────┐
       │                 │
       ▼                 ▼
┌──────────────┐  ┌─────────────────────┐
│ AirtableTo   │  │ AirtableImage       │
│ Postgres     │  │ Downloader          │
└──────┬───────┘  └──────┬──────────────┘
       │                 │
       ▼                 ▼
┌──────────────┐  ┌─────────────────────┐
│  PostgreSQL  │  │  Local File System  │
│   Database   │  │  (images/ dir)      │
└──────────────┘  └─────────────────────┘
```

## Target Tables

### ARTWORK_IMAGE Table
- Contains images related to artwork pieces
- Multiple images per artwork possible
- Images stored as Airtable attachments
- **ID Column**: Used in filename as `artwork_{id}_...`

### ARCHIVE_IMAGE Table
- Contains archival images
- Multiple images per archive item possible
- Images stored as Airtable attachments
- **ID Column**: Used in filename as `archive_{id}_...`

## Design Decisions

### Why Separate Projects?

1. **Separation of Concerns**
   - Data sync vs. file download are different operations
   - Different execution frequencies (data: frequent, images: once/occasionally)
   - Different resource requirements (database vs. disk space)

2. **Independent Scaling**
   - Image downloads can run independently
   - Don't block data syncs with large downloads
   - Can be run on different schedules

3. **Simpler Error Handling**
   - Image download failures don't affect data sync
   - Can retry images separately
   - Resume capability for interrupted downloads

### Why Not Store Images in Database?

1. **Storage Efficiency**
   - Images are large binary objects
   - File system is more appropriate than database BLOBs
   - Easier to serve via web server/CDN

2. **Performance**
   - Database queries aren't slowed by large BLOB columns
   - File system access is faster for images
   - Can leverage OS-level caching

3. **Portability**
   - Images can be moved/backed up separately
   - Easier to use with various tools and frameworks
   - Standard web serving doesn't require database queries

4. **Web access**
   - Images can uploaded to Amazon bucket 
   - Images can be accessed with a web browser and URL

## Image Storage Strategy

### Directory Structure
```
images/
├── artwork/
│   └── artwork_{recordId}_{size}.{ext}
└── archive/
    └── archive_{recordId}_{size}.{ext}
```

### Naming Convention Benefits

1. **Predictable**: Given a record ID, can construct filename
2. **Unique**: Record ID ensures no conflicts
3. **Informative**: Name contains table, ID, and size
4. **Sortable**: Groups by table prefix, then by ID
5. **Web-friendly**: No spaces or special characters

### Size Variants

Airtable provides multiple sizes:
- **Full**: Original uploaded image (width×height in filename)
- **Small**: Thumbnail (~200px)
- **Large**: Medium size (~1000px)

All variants are downloaded to support different use cases:
- Full: High-quality display, printing
- Large: Normal web display
- Small: Thumbnails, previews, lists

## Integration Points

### Current Integration: None
These projects currently run independently with no direct integration.

### Potential Future Integration

1. **Database References**
   - Store downloaded image paths in PostgreSQL
   - Link images to their respective records
   - Track download status and timestamps

2. **Shared Configuration**
   - Move common settings to shared config file
   - Centralized credential management
   - Unified logging

3. **Coordinated Execution**
   - Master script to run both operations
   - Check for new images after data sync
   - Download only images from newly synced records

4. **Web Application**
   - Use PostgreSQL data for metadata
   - Serve images from local file system
   - Combined view of data + images

## Technology Stack

### Common Technologies
- **.NET 10.0**: Both projects use latest .NET
- **Newtonsoft.Json**: JSON parsing for Airtable API
- **Microsoft.Extensions.Configuration**: Settings management

### Project-Specific Technologies

**AirtableToPostgres**:
- Npgsql: PostgreSQL driver
- Custom schema parsing
- Change detection logic
- Sync history tracking

**AirtableImageDownloader**:
- HttpClient: Image downloads
- File system operations
- Resume capability

## Security Considerations

### Credentials
- API keys stored in appsettings.json (not in source control)
- Should use .gitignore for appsettings.json
- Consider environment variables for production

### File Access
- Output directory should have appropriate permissions
- Consider disk space limits
- Validate filenames to prevent path traversal

### API Rate Limits
- Airtable has rate limits (5 requests/second)
- Current implementation is sequential (naturally rate-limited)
- May need throttling if parallelized in future

## Performance Characteristics

### AirtableImageDownloader

**Time Complexity**: O(n × m)
- n = number of records
- m = average attachments per record

**Space Complexity**: O(total image size)
- Limited by disk space
- No in-memory buffering of multiple images

**Network**:
- Bottleneck is typically download speed
- Each image requires separate HTTP request
- Could be parallelized for better performance

**Resume Capability**:
- File existence check adds negligible overhead
- Enables efficient re-runs
- Safe to interrupt at any time

## Deployment Considerations

### Development
- Run manually from command line
- Output to local images/ directory
- Suitable for testing and initial download

### Production Options

1. **Scheduled Task**
   - Windows Task Scheduler or cron job
   - Run periodically (e.g., nightly)
   - Check for new images

2. **On-Demand Service**
   - Trigger via API call
   - Run when new records added
   - Webhook from Airtable

3. **Container Deployment**
   - Docker container
   - Mount volume for persistent storage
   - Environment variables for config

## Monitoring and Logging

### Current Logging
- Console output only
- Shows progress in real-time
- Reports statistics at end

### Recommended Enhancements
- Write log file for historical record
- Track download times per image
- Alert on high failure rates
- Disk space monitoring
- Integration with logging service (e.g., Serilog)

## Backup Strategy

### Images
- Regular backups of images/ directory
- Consider cloud storage sync (S3, Azure Blob)
- Incremental backups (only new/changed files)

### Recovery
- Can always re-download from Airtable
- No data loss if images/ deleted
- Resume capability handles interruptions

## Scaling Considerations

### Current Scale
- Handles hundreds to thousands of records
- Sequential download approach
- Single-threaded operation

### Future Scaling Options

1. **Parallel Downloads**
   - Download multiple images simultaneously
   - Respect rate limits
   - 5-10 concurrent downloads recommended

2. **Distributed Processing**
   - Multiple instances with work queues
   - Shared file storage (NFS, cloud)
   - Coordinate via database

3. **Incremental Processing**
   - Track last run timestamp
   - Only fetch new/updated records
   - Filter by LAST_MODIFIED_TIME()

4. **CDN Integration**
   - Upload to CDN after download
   - Serve from edge locations
   - Reduce local storage needs
