using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Apollo.Core;
using Apollo.Elements;
using Apollo.Structures;

namespace Apollo.Devices {
    public class Multi: Device, IChainParent {
        public static readonly new string DeviceIdentifier = "multi";

        private Action<Signal> _midiexit;
        public override Action<Signal> MIDIExit {
            get => _midiexit;
            set {
                _midiexit = value;
                Reroute();
            }
        }

        public Chain Preprocess;
        private List<Chain> _chains = new List<Chain>();

        private Random RNG = new Random();
        
        public enum MultiType {
            Forward,
            Backward,
            Random,
            RandomPlus
        }

        private MultiType _mode;
        public string Mode {
            get {
                if (_mode == MultiType.Forward) return "Forward";
                else if (_mode == MultiType.Backward) return "Backward";
                else if (_mode == MultiType.Random) return "Random";
                else if (_mode == MultiType.RandomPlus) return "Random+";
                return null;
            }
            set {
                if (value == "Forward") _mode = MultiType.Forward;
                else if (value == "Backward") _mode = MultiType.Backward;
                else if (value == "Random") _mode = MultiType.Random;
                else if (value == "Random+") _mode = MultiType.RandomPlus;
            }
        }

        public MultiType GetMultiMode() => _mode;

        private int current = -1;
        private ConcurrentDictionary<Signal, int> buffer = new ConcurrentDictionary<Signal, int>();

        private void Reroute() {
            Preprocess.Parent = this;
            Preprocess.MIDIExit = PreprocessExit;

            for (int i = 0; i < _chains.Count; i++) {
                _chains[i].Parent = this;
                _chains[i].ParentIndex = i;
                _chains[i].MIDIExit = ChainExit;
            }
        }

        public Chain this[int index] {
            get => _chains[index];
        }

        public int Count {
            get => _chains.Count;
        }

        public override Device Clone() => new Multi(Preprocess.Clone(), (from i in _chains select i.Clone()).ToList(), _mode, Expanded);

        public void Insert(int index, Chain chain = null) {
            _chains.Insert(index, chain?? new Chain());
            
            Reroute();
        }

        public void Add(Chain chain) {
            _chains.Add(chain);

            Reroute();
        }

        public void Remove(int index) {
            _chains.RemoveAt(index);

            Reroute();
        }

        private void Reset() => current = -1;

        public int? Expanded;

        public Multi(Chain preprocess = null, List<Chain> init = null, MultiType mode = MultiType.Forward, int? expanded = null): base(DeviceIdentifier) {
            Preprocess = preprocess?? new Chain();

            foreach (Chain chain in init?? new List<Chain>()) _chains.Add(chain);

            _mode = mode;
            
            Expanded = expanded;
            
            Launchpad.MultiReset += Reset;

            Reroute();
        }

        private void ChainExit(Signal n) => MIDIExit?.Invoke(n);

        public override void MIDIEnter(Signal n) {
            Signal m = n.Clone();
            n.Color = new Color();

            if (!buffer.ContainsKey(n)) {
                if (!m.Color.Lit) return;

                if (_mode == MultiType.Forward) {
                    if (++current >= _chains.Count) current = 0;
                
                } else if (_mode == MultiType.Backward) {
                    if (--current < 0) current = _chains.Count - 1;
                
                } else if (_mode == MultiType.Random || current == -1)
                    current = RNG.Next(_chains.Count);
                
                else if (_mode == MultiType.RandomPlus) {
                    int old = current;
                    current = RNG.Next(_chains.Count - 1);
                    if (current >= old) current++;
                }

                m.MultiTarget = buffer[n] = current;

            } else {
                m.MultiTarget = buffer[n];
                if (!m.Color.Lit) buffer.Remove(n, out int _);
            }

            Preprocess.MIDIEnter(m);
        }

        private void PreprocessExit(Signal n) {
            int target = n.MultiTarget.Value;
            n.MultiTarget = null;
            
            if (_chains.Count == 0) {
                MIDIExit?.Invoke(n);
                return;
            }
            
            _chains[target].MIDIEnter(n);
        }

        public override void Dispose() {
            Preprocess.Dispose();
            foreach (Chain chain in _chains) chain.Dispose();
            base.Dispose();
        }
    }
}