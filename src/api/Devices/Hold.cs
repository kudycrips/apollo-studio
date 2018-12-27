using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

using api;

namespace api.Devices {
    public class Hold: Device {
        public static readonly new string DeviceIdentifier = "hold";

        public bool Mode; // true uses Length
        public Length Length;
        private int _time;
        private Decimal _gate;

        private object locker = new object();

        private Queue<Timer> _timers = new Queue<Timer>();
        private TimerCallback _timerexit;

        public int Time {
            get {
                return _time;
            }
            set {
                if (10 <= value && value <= 30000)
                    _time = value;
            }
        }

        public Decimal Gate {
            get {
                return _gate;
            }
            set {
                if (0 <= value && value <= 4)
                    _gate = value;
            }
        }

        public override Device Clone() {
            return new Hold(Mode, Length, _time, _gate);
        }

        public Hold(bool mode = false, Length length = null, int time = 500, Decimal gate = 1): base(DeviceIdentifier) {
            _timerexit = new TimerCallback(Tick);

            if (length == null) length = new Length();

            Mode = mode;
            Time = time;
            Length = length;
            Gate = gate;
        }

        private void Tick(object info) {
            if (info.GetType() == typeof(byte)) {
                Signal n = new Signal((byte)info, new Color(0));

                lock (locker) {
                    if (MIDIExit != null)
                        MIDIExit(n);
                    
                    _timers.Dequeue();
                }
            }
        }

        public override void MIDIEnter(Signal n) {
            if (n.Color.Lit) {
                _timers.Enqueue(new Timer(_timerexit, n.Index, Convert.ToInt32((Mode? (int)Length : _time) * _gate), Timeout.Infinite));
                
                if (MIDIExit != null)
                    MIDIExit(n);
            }
        }

        public static Device DecodeSpecific(string jsonString) {
            Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            if (json["device"].ToString() != DeviceIdentifier) return null;

            Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json["data"].ToString());
            
            return new Hold(Convert.ToBoolean(data["mode"]), new Length(Convert.ToDecimal(data["length"])), Convert.ToInt32(data["time"]), Convert.ToInt32(data["gate"]));
        }

        public override string EncodeSpecific() {
            StringBuilder json = new StringBuilder();

            using (JsonWriter writer = new JsonTextWriter(new StringWriter(json))) {
                writer.WriteStartObject();

                    writer.WritePropertyName("device");
                    writer.WriteValue(DeviceIdentifier);

                    writer.WritePropertyName("data");
                    writer.WriteStartObject();

                        writer.WritePropertyName("mode");
                        writer.WriteValue(Mode);

                        writer.WritePropertyName("length");
                        writer.WriteValue(Convert.ToInt32(Math.Log(Convert.ToDouble(Length.Value), 2)) + 7);

                        writer.WritePropertyName("time");
                        writer.WriteValue(_time);

                        writer.WritePropertyName("gate");
                        writer.WriteValue(_gate);

                    writer.WriteEndObject();

                writer.WriteEndObject();
            }
            
            return json.ToString();
        }

        public override ObjectResult RespondSpecific(string jsonString) {
            Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            if (json["object"].ToString() != "message") return new BadRequestObjectResult("Not a message.");
            if (json["recipient"].ToString() != Identifier) return new BadRequestObjectResult("Incorrect recipient for message.");
            if (json["device"].ToString() != DeviceIdentifier) return new BadRequestObjectResult("Incorrect device recipient for message.");

            Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json["data"].ToString());

            switch (data["type"].ToString()) {
                case "forward":
                    return new BadRequestObjectResult("The Hold object has no members to forward to.");
                
                case "mode":
                    Mode = Convert.ToBoolean(data["value"]);
                    return new OkObjectResult(EncodeSpecific());

                case "length":
                    Length = new Length(Convert.ToInt32(data["value"]) - 7);
                    return new OkObjectResult(EncodeSpecific());

                case "time":
                    Time = Convert.ToInt32(data["value"]);
                    return new OkObjectResult(EncodeSpecific());

                case "gate":
                    Gate = Convert.ToDecimal(data["value"]);
                    return new OkObjectResult(EncodeSpecific());
                
                default:
                    return new BadRequestObjectResult("Unknown message type.");
            }
        }
    }
}