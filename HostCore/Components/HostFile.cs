using System;

namespace HostCore.Components
{
    [Serializable]
    public class HostFile
    {
        public string Path;
        public string Description;
        public string DisplayCharacter;
        public bool PointsToLive;
        public bool IsDelete;
    }
}
