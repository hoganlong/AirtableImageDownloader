# Quick Start Guide

## What This Does

Downloads all images from two Airtable tables (ARTWORK_IMAGE and ARCHIVE_IMAGE) and saves them to your local disk with organized naming.

## 5-Minute Setup

### 1. Navigate to Project
```bash
cd AirtableImageDownloader
```

### 2. Verify Configuration
Check `appsettings.json` - should already be configured:
```json
{
  "Airtable": {
    "ApiKey": "patAqas0D0SfJ7LqG...",
    "BaseId": "appnGXIULYw1ZA6bF"
  }
}
```

### 3. Run
```bash
dotnet run
```

That's it! Images will download to `images/artwork/` and `images/archive/`.

## What Happens

1. Connects to Airtable
2. Fetches all records from ARTWORK_IMAGE table
3. Downloads images as: `artwork_{recordId}_{size}.ext`
4. Fetches all records from ARCHIVE_IMAGE table
5. Downloads images as: `archive_{recordId}_{size}.ext`
6. Skips files that already exist (resume-capable)

## Output Example

```
images/
├── artwork/
│   ├── artwork_recA1B2C3_1920x1080.jpg
│   ├── artwork_recA1B2C3_small_200x150.jpg
│   └── artwork_recX7Y8Z9_2048x1536.png
└── archive/
    ├── archive_rec123ABC_1600x1200.jpg
    └── archive_rec123ABC_large_1000x750.jpg
```

## Resume After Interruption

Just run `dotnet run` again. It will:
- ✓ Skip files that already exist
- ✓ Download only missing images
- ✓ Pick up where it left off

## Common Issues

**"Error: Airtable ApiKey and BaseId must be configured"**
→ Check `appsettings.json` exists in the project directory

**No images downloaded**
→ Verify table names are correct (ARTWORK_IMAGE and ARCHIVE_IMAGE)

**Permission denied**
→ Ensure write permissions in the project directory

## Related Project

**AirtableToPostgres** (in parent directory)
- Syncs Airtable data to PostgreSQL
- Same Airtable base, different purpose
- See that project's README for data sync

## More Info

- **README.md** - Full documentation
- **ARCHITECTURE.md** - System design and technical details
- **Program.cs** - Source code with comments
