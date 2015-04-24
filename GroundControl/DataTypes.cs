using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GroundControl
{
    [XmlRoot("sync")]
    public class RocketProject
    {
        [XmlElement("TimeFormat")]
        public string TimeFormat = "{row}";

        [XmlElement("AudioFile")]
        public string AudioFile;

        [XmlElement("AudioOffset")]
        public int AudioOffset;

        [XmlElement("BPM")]
        public int BPM = 120;

        [XmlElement("RowsPerBeat")]
        public int RowsPerBeat = 8;

        [XmlAttribute("rows")]
        public int Rows = 4096;

        [XmlArrayItem("track", typeof(TrackInfo))]
        [XmlArray("tracks")]
        public List<TrackInfo> Tracks = new List<TrackInfo>();

        [XmlArrayItem("bookmark", typeof(Bookmark))]
        [XmlArray("bookmarks")]
        public List<Bookmark> Bookmarks = new List<Bookmark>();
    }

    public class TrackInfo
    {
        [XmlAttribute("name")]
        public string Name;

        [XmlAttribute("visible")]
        public bool Visible = true;

        [XmlElement("key")]
        public List<KeyInfo> Keys = new List<KeyInfo>();
    }

    public class KeyInfo : IComparable<KeyInfo>
    {
        [XmlAttribute("interpolation")]
        public int Interpolation;

        [XmlAttribute("row")]
        public int Row;

        [XmlAttribute("value")]
        public float Value;

        public int CompareTo(KeyInfo other)
        {
            return this.Row.CompareTo(other.Row);
        }
    }

    public class Bookmark
    {
        [XmlAttribute("row")]
        public int Row;

        [XmlAttribute("number")]
        public int Number = -1;
    }
}
