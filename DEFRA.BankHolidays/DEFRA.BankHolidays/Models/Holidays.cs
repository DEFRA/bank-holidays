using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DEFRA.BankHolidays.Models
{    
    public class Holidays
    {
        [JsonIgnore]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "england-and-wales")]
        public EnglandWales EnglandWales { get; set; }
        public Scotland Scotland { get; set; }

        [JsonProperty(PropertyName = "northern-ireland")]
        public NorthernIreland NorthernIreland { get; set; }
    }

    public class EnglandWales
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Division { get; set; }
        public List<Event> Events { get; set; }
    }

    public class Scotland
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Division { get; set; }
        public List<Event> Events { get; set; }
    }

    public class NorthernIreland
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Division { get; set; }
        public List<Event> Events { get; set; }
    }

    public class Event
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Notes { get; set; }
        public bool Bunting { get; set; }
    }
}