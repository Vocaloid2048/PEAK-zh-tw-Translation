using System.Collections.Generic;

namespace PEAK.TxtUpdater
{
    public class Manifest
    {
        public string generated_at { get; set; }
        public string @ref { get; set; }
        public string base_raw_url { get; set; }
        public List<ManifestFile> files { get; set; }
    }

    public class ManifestFile
    {
        public string name { get; set; }
        public string path { get; set; }
        public string sha256 { get; set; }
    }
}
