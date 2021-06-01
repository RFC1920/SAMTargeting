# SAM Targeting for Rust
Make SamSites target players, NPCs, and animals

## Configuration
```json
{
  "Player targeting by SamSite": true,
  "NPC targeting by SamSite": true,
  "Animal targeting by SamSite": true,
  "Player targeting by NPC SamSite": false,
  "NPC targeting by NPC SamSite": false,
  "Animal targeting by NPC SamSite": false,
  "Animals to exclude": [
    "chicken"
  ],
  "SamSite Range": 150.0,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 1
  }
}
```

Note that you can exclude certain animals by the short name, e.g. chicken, bear, boar, stag.

Also, the only difference between a SamSite and an NPC SamSite is the owner.  OwnerID 0 is server and would be reserved for SAMSites at Launch Site, etc.
