using System;
using System.Collections.Generic;
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
        
        [XmlElement("LightTheme")]
        public bool LightTheme;

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

        public int FindKeyByRow(int row, bool includePrevKey = false)
        {
            // Search for key
            var index = Keys.BinarySearch(new KeyInfo() { Row = row });

            // Is it an exact find?
            if (index >= 0)
                return index;
            else
                return includePrevKey ? ~index - 1 : -1;
        }

        public float GetValue(float row, out float sinceRows)
        {
            // If we have no keys at all, return a constant 0 
            if (Keys.Count == 0)
            {
                sinceRows = row;
                return 0.0f;
            }

            // find key at/before the current row
            var index = FindKeyByRow((int)row, true);

            // is "row" before the first key?
            if (index < 0)
            {
                sinceRows = row - Keys[0].Row;
                return Keys[0].Value;
            }
            
            // did we get the last key?
            if (index == Keys.Count - 1)
            {
                sinceRows = row - Keys[index].Row;
                return Keys[index].Value;
            }

            // interpolate according to key-type 
            sinceRows = row - Keys[index].Row;
            float t = sinceRows / (Keys[index + 1].Row - Keys[index].Row);
            switch (Keys[index].Interpolation)
            {
                case 0/*Step*/:
                    return Keys[index].Value;

                case 2 /*Smooth*/:
                    t = t * t * (3 - 2 * t);
                    break;

                case 3/*Ramp*/:
                    t = t * t;
                    break;
            }
            return Keys[index].Value + (Keys[index + 1].Value - Keys[index].Value) * t;
        }

        public float GetValue(float row)
        {
            float temp;
            return GetValue(row, out temp);
        }
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
