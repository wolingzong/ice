//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

namespace IceInternal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;

    internal interface IMetricsMap
    {
        IceMX.Metrics[] getMetrics();
        IceMX.MetricsFailures[]? getFailures();
        IceMX.MetricsFailures? getFailures(string id);
        Dictionary<string, string> getProperties();
    }

    internal interface ISubMap
    {
        void addSubMapToMetrics(IceMX.Metrics metrics);
    }

    internal interface ISubMapCloneFactory
    {
        ISubMap create();
    }

    internal interface ISubMapFactory
    {
        ISubMapCloneFactory createCloneFactory(string subMapPrefix, Ice.Communicator communicator);
    }

    internal interface IMetricsMapFactory
    {
        void registerSubMap<S>(string subMap, System.Reflection.FieldInfo field) where S : IceMX.Metrics, new();
        void update();
        IMetricsMap create(string mapPrefix, Ice.Communicator communicator);
    }

    internal class SubMap<S> : ISubMap where S : IceMX.Metrics, new()
    {
        internal SubMap(MetricsMap<S> map, System.Reflection.FieldInfo field)
        {
            _map = map;
            _field = field;
        }

        internal MetricsMap<S>.Entry? getMatching(IceMX.MetricsHelper<S> helper)
        {
            return _map.getMatching(helper, null);
        }

        public void addSubMapToMetrics(IceMX.Metrics metrics)
        {
            try
            {
                _field.SetValue(metrics, _map.getMetrics());
            }
            catch (Exception)
            {
                Debug.Assert(false);
            }
        }

        private readonly MetricsMap<S> _map;
        private readonly System.Reflection.FieldInfo _field;
    }

    internal class SubMapCloneFactory<S> : ISubMapCloneFactory where S : IceMX.Metrics, new()
    {
        internal SubMapCloneFactory(MetricsMap<S> map, System.Reflection.FieldInfo field)
        {
            _map = map;
            _field = field;
        }

        public ISubMap create()
        {
            return new SubMap<S>(new MetricsMap<S>(_map), _field);
        }

        private readonly MetricsMap<S> _map;
        private readonly System.Reflection.FieldInfo _field;
    }

    internal class SubMapFactory<S> : ISubMapFactory where S : IceMX.Metrics, new()
    {
        internal SubMapFactory(System.Reflection.FieldInfo field)
        {
            _field = field;
        }

        public ISubMapCloneFactory createCloneFactory(string subMapPrefix, Ice.Communicator communicator)
        {
            return new SubMapCloneFactory<S>(new MetricsMap<S>(subMapPrefix, communicator, null), _field);
        }

        private readonly System.Reflection.FieldInfo _field;
    }

    public class MetricsMap<T> : IMetricsMap where T : IceMX.Metrics, new()
    {
        public class Entry
        {
            internal Entry(MetricsMap<T> map, T obj)
            {
                _map = map;
                _object = obj;
            }

            public void failed(string exceptionName)
            {
                lock (_map)
                {
                    ++_object.failures;
                    int count;
                    if (_failures == null)
                    {
                        _failures = new Dictionary<string, int>();
                    }
                    if (_failures.TryGetValue(exceptionName, out count))
                    {
                        _failures[exceptionName] = count + 1;
                    }
                    else
                    {
                        _failures[exceptionName] = 1;
                    }
                }
            }

            internal MetricsMap<S>.Entry? getMatching<S>(string mapName, IceMX.MetricsHelper<S> helper)
                where S : IceMX.Metrics, new()
            {
                ISubMap? m;
                lock (_map)
                {
                    if (_subMaps == null || !_subMaps.TryGetValue(mapName, out m))
                    {
                        m = _map.createSubMap(mapName);
                        if (m == null)
                        {
                            return null;
                        }
                        if (_subMaps == null)
                        {
                            _subMaps = new Dictionary<string, ISubMap>();
                        }
                        _subMaps.Add(mapName, m);
                    }
                }
                return ((SubMap<S>)m).getMatching(helper);
            }

            public void detach(long lifetime)
            {
                lock (_map)
                {
                    _object.totalLifetime += lifetime;
                    if (--_object.current == 0)
                    {
                        _map.detached(this);
                    }
                }
            }

            public void execute(IceMX.Observer<T>.MetricsUpdate func)
            {
                lock (_map)
                {
                    func(_object);
                }
            }

            public MetricsMap<T> getMap()
            {
                return _map;
            }

            internal IceMX.MetricsFailures? getFailures()
            {
                if (_failures == null)
                {
                    return null;
                }
                IceMX.MetricsFailures f = new IceMX.MetricsFailures();
                f.id = _object.id;
                f.failures = new Dictionary<string, int>(_failures);
                return f;
            }

            internal void attach(IceMX.MetricsHelper<T> helper)
            {
                ++_object.total;
                ++_object.current;
                helper.initMetrics(_object);
            }

            internal bool isDetached()
            {
                return _object.current == 0;
            }

            internal IceMX.Metrics clone()
            {
                T metrics = (T)_object.Clone();
                if (_subMaps != null)
                {
                    foreach (ISubMap s in _subMaps.Values)
                    {
                        s.addSubMapToMetrics(metrics);
                    }
                }
                return metrics;
            }

            internal string getId()
            {
                return _object.id;
            }

            private MetricsMap<T> _map;
            private T _object;
            private Dictionary<string, int>? _failures;
            private Dictionary<string, ISubMap>? _subMaps;
        }

        internal MetricsMap(string mapPrefix, Ice.Communicator communicator, Dictionary<string, ISubMapFactory>? subMaps)
        {
            MetricsAdminI.validateProperties(mapPrefix, communicator);
            _properties = communicator.GetProperties(forPrefix: mapPrefix);

            _retain = communicator.GetPropertyAsInt($"{mapPrefix}RetainDetached") ?? 10;
            _accept = parseRule(communicator, $"{mapPrefix}Accept");
            _reject = parseRule(communicator, $"{mapPrefix}Reject");
            _groupByAttributes = new List<string>();
            _groupBySeparators = new List<string>();

            string groupBy = communicator.GetProperty($"{mapPrefix}GroupBy") ?? "id";
            if (groupBy.Length > 0)
            {
                string v = "";
                bool attribute = char.IsLetter(groupBy[0]) || char.IsDigit(groupBy[0]);
                if (!attribute)
                {
                    _groupByAttributes.Add("");
                }

                foreach (char p in groupBy)
                {
                    bool isAlphaNum = char.IsLetter(p) || char.IsDigit(p) || p == '.';
                    if (attribute && !isAlphaNum)
                    {
                        _groupByAttributes.Add(v);
                        v = "" + p;
                        attribute = false;
                    }
                    else if (!attribute && isAlphaNum)
                    {
                        _groupBySeparators.Add(v);
                        v = "" + p;
                        attribute = true;
                    }
                    else
                    {
                        v += p;
                    }
                }

                if (attribute)
                {
                    _groupByAttributes.Add(v);
                }
                else
                {
                    _groupBySeparators.Add(v);
                }
            }

            if (subMaps != null && subMaps.Count > 0)
            {
                _subMaps = new Dictionary<string, ISubMapCloneFactory>();

                List<string> subMapNames = new List<string>();
                foreach (KeyValuePair<string, ISubMapFactory> e in subMaps)
                {
                    subMapNames.Add(e.Key);
                    string subMapsPrefix = mapPrefix + "Map.";
                    string subMapPrefix = subMapsPrefix + e.Key + '.';
                    if (communicator.GetProperties(forPrefix: subMapPrefix).Count == 0)
                    {
                        if (communicator.GetProperties(forPrefix: subMapsPrefix).Count == 0)
                        {
                            subMapPrefix = mapPrefix;
                        }
                        else
                        {
                            continue; // This sub-map isn't configured.
                        }
                    }

                    _subMaps.Add(e.Key, e.Value.createCloneFactory(subMapPrefix, communicator));
                }
            }
            else
            {
                _subMaps = null;
            }
        }

        internal MetricsMap(MetricsMap<T> map)
        {
            _properties = map._properties;
            _groupByAttributes = map._groupByAttributes;
            _groupBySeparators = map._groupBySeparators;
            _retain = map._retain;
            _accept = map._accept;
            _reject = map._reject;
            _subMaps = map._subMaps;
        }

        public Dictionary<string, string> getProperties()
        {
            return _properties;
        }

        public IceMX.Metrics[] getMetrics()
        {
            lock (this)
            {
                IceMX.Metrics[] metrics = new IceMX.Metrics[_objects.Count];
                int i = 0;
                foreach (Entry e in _objects.Values)
                {
                    metrics[i++] = e.clone();
                }
                return metrics;
            }
        }

        public IceMX.MetricsFailures[] getFailures()
        {
            lock (this)
            {
                List<IceMX.MetricsFailures> failures = new List<IceMX.MetricsFailures>();
                foreach (Entry e in _objects.Values)
                {
                    IceMX.MetricsFailures? f = e.getFailures();
                    if (f != null)
                    {
                        failures.Add(f);
                    }
                }
                return failures.ToArray();
            }
        }

        public IceMX.MetricsFailures? getFailures(string id)
        {
            lock (this)
            {
                Entry e;
                if (_objects.TryGetValue(id, out e))
                {
                    return e.getFailures();
                }
                return null;
            }
        }

        internal ISubMap? createSubMap(string subMapName)
        {
            if (_subMaps == null)
            {
                return null;
            }
            ISubMapCloneFactory factory;
            if (_subMaps.TryGetValue(subMapName, out factory))
            {
                return factory.create();
            }
            return null;
        }

        public Entry? getMatching(IceMX.MetricsHelper<T> helper, Entry? previous)
        {
            //
            // Check the accept and reject filters.
            //
            foreach (KeyValuePair<string, Regex> e in _accept)
            {
                if (!match(e.Key, e.Value, helper, false))
                {
                    return null;
                }
            }

            foreach (KeyValuePair<string, Regex> e in _reject)
            {
                if (match(e.Key, e.Value, helper, true))
                {
                    return null;
                }
            }

            //
            // Compute the key from the GroupBy property.
            //
            string key;
            try
            {
                if (_groupByAttributes.Count == 1)
                {
                    key = helper.resolve(_groupByAttributes[0]);
                }
                else
                {
                    StringBuilder os = new StringBuilder();
                    IEnumerator<string> q = _groupBySeparators.GetEnumerator();
                    foreach (string p in _groupByAttributes)
                    {
                        os.Append(helper.resolve(p));
                        if (q.MoveNext())
                        {
                            os.Append(q.Current);
                        }
                    }
                    key = os.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }

            //
            // Lookup the metrics object.
            //
            lock (this)
            {
                if (previous != null && previous.getId().Equals(key))
                {
                    Debug.Assert(_objects[key] == previous);
                    return previous;
                }

                Entry e;
                if (!_objects.TryGetValue(key, out e))
                {
                    try
                    {
                        T t = new T();
                        t.id = key;
                        e = new Entry(this, t);
                        _objects.Add(key, e);
                    }
                    catch (Exception)
                    {
                        Debug.Assert(false);
                    }
                }
                e.attach(helper);
                return e;
            }
        }

        private void detached(Entry entry)
        {
            if (_retain == 0)
            {
                return;
            }

            if (_detachedQueue == null)
            {
                _detachedQueue = new LinkedList<Entry>();
            }
            Debug.Assert(_detachedQueue.Count <= _retain);

            // Compress the queue by removing entries which are no longer detached.
            LinkedListNode<Entry> p = _detachedQueue.First;
            while (p != null)
            {
                LinkedListNode<Entry> next = p.Next;
                if (p.Value == entry || !p.Value.isDetached())
                {
                    _detachedQueue.Remove(p);
                }
                p = next;
            }

            // If there's still no room, remove the oldest entry (at the front).
            if (_detachedQueue.Count == _retain)
            {
                _objects.Remove(_detachedQueue.First.Value.getId());
                _detachedQueue.RemoveFirst();
            }

            // Add the entry at the back of the queue.
            _detachedQueue.AddLast(entry);
        }

        private Dictionary<string, Regex> parseRule(Ice.Communicator communicator, string name)
        {
            Dictionary<string, Regex> pats = new Dictionary<string, Regex>();
            Dictionary<string, string> rules = communicator.GetProperties(forPrefix: $"{name}.");
            foreach (KeyValuePair<string, string> e in rules)
            {
                pats.Add(e.Key.Substring(name.Length + 1), new Regex(e.Value));
            }
            return pats;
        }

        private bool match(string attribute, Regex regex, IceMX.MetricsHelper<T> helper, bool reject)
        {
            string value;
            try
            {
                value = helper.resolve(attribute);
            }
            catch (Exception)
            {
                return !reject;
            }
            return regex.IsMatch(value);
        }

        private readonly Dictionary<string, string> _properties;
        private readonly List<string> _groupByAttributes;
        private readonly List<string> _groupBySeparators;
        private readonly int _retain;
        private readonly Dictionary<string, Regex> _accept;
        private readonly Dictionary<string, Regex> _reject;

        private readonly Dictionary<string, Entry> _objects = new Dictionary<string, Entry>();
        private readonly Dictionary<string, ISubMapCloneFactory>? _subMaps;
        private LinkedList<Entry>? _detachedQueue;
    }

    internal class MetricsViewI
    {
        internal MetricsViewI(string name)
        {
            _name = name;
        }

        internal bool addOrUpdateMap(Ice.Communicator communicator, string mapName, IMetricsMapFactory factory,
                                   Ice.Logger logger)
        {
            //
            // Add maps to views configured with the given map.
            //
            string viewPrefix = "IceMX.Metrics." + _name + ".";
            string mapsPrefix = viewPrefix + "Map.";
            Dictionary<string, string> mapsProps = communicator.GetProperties(forPrefix: mapsPrefix);

            string mapPrefix;
            Dictionary<string, string> mapProps = new Dictionary<string, string>();
            if (mapsProps.Count > 0)
            {
                mapPrefix = mapsPrefix + mapName + ".";
                mapProps = communicator.GetProperties(forPrefix: mapPrefix);
                if (mapProps.Count == 0)
                {
                    // This map isn't configured for this view.
                    return _maps.Remove(mapName);
                }
            }
            else
            {
                mapPrefix = viewPrefix;
                mapProps = communicator.GetProperties(forPrefix: mapPrefix);
            }

            if (communicator.GetPropertyAsInt($"{mapPrefix}Disabled") > 0)
            {
                // This map is disabled for this view.
                return _maps.Remove(mapName);
            }

            IMetricsMap m;
            if (_maps.TryGetValue(mapName, out m) && Ice.Collections.Equals(m.getProperties(), mapProps))
            {
                return false; // The map configuration didn't change, no need to re-create.
            }

            try
            {
                _maps[mapName] = factory.create(mapPrefix, communicator);
            }
            catch (Exception ex)
            {
                logger.warning("unexpected exception while creating metrics map:\n" + ex);
                _maps.Remove(mapName);
            }
            return true;
        }

        internal bool removeMap(string mapName)
        {
            return _maps.Remove(mapName);
        }

        internal Dictionary<string, IceMX.Metrics[]> getMetrics()
        {
            Dictionary<string, IceMX.Metrics[]> metrics = new Dictionary<string, IceMX.Metrics[]>();
            foreach (KeyValuePair<string, IMetricsMap> e in _maps)
            {
                IceMX.Metrics[] m = e.Value.getMetrics();
                if (m != null)
                {
                    metrics.Add(e.Key, m);
                }
            }
            return metrics;
        }

        internal IceMX.MetricsFailures[]? getFailures(string mapName)
        {
            IMetricsMap m;
            if (_maps.TryGetValue(mapName, out m))
            {
                return m.getFailures();
            }
            return null;
        }

        internal IceMX.MetricsFailures? getFailures(string mapName, string id)
        {
            IMetricsMap m;
            if (_maps.TryGetValue(mapName, out m))
            {
                return m.getFailures(id);
            }
            return null;
        }

        internal ICollection<string> getMaps()
        {
            return _maps.Keys;
        }

        internal MetricsMap<T>? getMap<T>(string mapName) where T : IceMX.Metrics, new()
        {
            IMetricsMap m;
            if (_maps.TryGetValue(mapName, out m))
            {
                return (MetricsMap<T>)m;
            }
            return null;
        }

        private readonly string _name;
        private readonly Dictionary<string, IMetricsMap> _maps = new Dictionary<string, IMetricsMap>();
    }

    public class MetricsAdminI : IceMX.MetricsAdmin
    {
        private static readonly string[] suffixes =
            {
                "Disabled",
                "GroupBy",
                "Accept.*",
                "Reject.*",
                "RetainDetached",
                "Map.*",
            };

        public static void validateProperties(string prefix, Ice.Communicator communicator)
        {
            Dictionary<string, string> props = communicator.GetProperties(forPrefix: prefix);
            List<string> unknownProps = new List<string>();
            foreach (string prop in props.Keys)
            {
                bool valid = false;
                foreach (string suffix in suffixes)
                {
                    if (IceUtilInternal.StringUtil.match(prop, prefix + suffix, false))
                    {
                        valid = true;
                        break;
                    }
                }

                if (!valid)
                {
                    unknownProps.Add(prop);
                }
            }

            if (unknownProps.Count != 0 && (communicator.GetPropertyAsInt("Ice.Warn.UnknownProperties") ?? 1) > 0)
            {
                StringBuilder message = new StringBuilder("found unknown IceMX properties for `");
                message.Append(prefix.Substring(0, prefix.Length - 1));
                message.Append("':");
                foreach (string p in unknownProps)
                {
                    message.Append("\n    ");
                    message.Append(p);
                }
                Ice.Util.getProcessLogger().warning(message.ToString());
            }
        }

        private class MetricsMapFactory<T> : IMetricsMapFactory where T : IceMX.Metrics, new()
        {
            public MetricsMapFactory(Action updater)
            {
                _updater = updater;
            }

            public void update()
            {
                Debug.Assert(_updater != null);
                _updater();
            }

            public IMetricsMap create(string mapPrefix, Ice.Communicator communicator)
            {
                return new MetricsMap<T>(mapPrefix, communicator, _subMaps);
            }

            public void registerSubMap<S>(string subMap, System.Reflection.FieldInfo field)
                where S : IceMX.Metrics, new()
            {
                _subMaps.Add(subMap, new SubMapFactory<S>(field));
            }

            private readonly Action _updater;
            private readonly Dictionary<string, ISubMapFactory> _subMaps = new Dictionary<string, ISubMapFactory>();
        }

        public MetricsAdminI(Ice.Communicator communicator, Ice.Logger logger)
        {
            _logger = logger;
            _communicator = communicator;
            updateViews();
        }

        public void updateViews()
        {
            HashSet<IMetricsMapFactory> updatedMaps = new HashSet<IMetricsMapFactory>();
            lock (this)
            {
                string viewsPrefix = "IceMX.Metrics.";
                Dictionary<string, string> viewsProps = _communicator.GetProperties(forPrefix: viewsPrefix);
                Dictionary<string, MetricsViewI> views = new Dictionary<string, MetricsViewI>();
                _disabledViews.Clear();
                foreach (KeyValuePair<string, string> e in viewsProps)
                {
                    string viewName = e.Key.Substring(viewsPrefix.Length);
                    int dotPos = viewName.IndexOf('.');
                    if (dotPos > 0)
                    {
                        viewName = viewName.Substring(0, dotPos);
                    }

                    if (views.ContainsKey(viewName) || _disabledViews.Contains(viewName))
                    {
                        continue; // View already configured.
                    }

                    validateProperties($"{viewsPrefix}{viewName}.", _communicator);

                    if (_communicator.GetPropertyAsInt($"{viewsPrefix}{viewName}.Disabled") > 0)
                    {
                        _disabledViews.Add(viewName);
                        continue; // The view is disabled
                    }

                    //
                    // Create the view or update it.
                    //
                    MetricsViewI v;
                    if (!_views.TryGetValue(viewName, out v))
                    {
                        v = new MetricsViewI(viewName);
                    }
                    views[viewName] = v;

                    foreach (KeyValuePair<string, IMetricsMapFactory> f in _factories)
                    {
                        if (v.addOrUpdateMap(_communicator, f.Key, f.Value, _logger))
                        {
                            updatedMaps.Add(f.Value);
                        }
                    }
                }

                Dictionary<string, MetricsViewI> tmp = _views;
                _views = views;
                views = tmp;

                //
                // Go through removed views to collect maps to update.
                //
                foreach (KeyValuePair<string, MetricsViewI> v in views)
                {
                    if (!_views.ContainsKey(v.Key))
                    {
                        foreach (string n in v.Value.getMaps())
                        {
                            updatedMaps.Add(_factories[n]);
                        }
                    }
                }
            }

            //
            // Call the updaters to update the maps.
            //
            foreach (IMetricsMapFactory f in updatedMaps)
            {
                f.update();
            }
        }

        public string[] GetMetricsViewNames(out string[] disabledViews, Ice.Current current)
        {
            lock (this)
            {
                disabledViews = _disabledViews.ToArray();
                return new List<string>(_views.Keys).ToArray();
            }
        }

        public void EnableMetricsView(string name, Ice.Current current)
        {
            lock (this)
            {
                getMetricsView(name); // Throws if unknown view.
                _communicator.SetProperty($"IceMX.Metrics.{name}.Disabled", "0");
            }
            updateViews();
        }

        public void DisableMetricsView(string name, Ice.Current current)
        {
            lock (this)
            {
                getMetricsView(name); // Throws if unknown view.
                _communicator.SetProperty($"IceMX.Metrics.{name}.Disabled", "1");
            }
            updateViews();
        }

        public Dictionary<string, IceMX.Metrics[]> GetMetricsView(string viewName, out long timestamp, Ice.Current current)
        {
            lock (this)
            {
                MetricsViewI? view = getMetricsView(viewName);
                timestamp = Time.currentMonotonicTimeMillis();
                if (view != null)
                {
                    return view.getMetrics();
                }
                return new Dictionary<string, IceMX.Metrics[]>();
            }
        }

        public IceMX.MetricsFailures[]? GetMapMetricsFailures(string viewName, string mapName, Ice.Current c)
        {
            lock (this)
            {
                MetricsViewI? view = getMetricsView(viewName);
                if (view != null)
                {
                    return view.getFailures(mapName);
                }
                return Array.Empty<IceMX.MetricsFailures>();
            }
        }

        public IceMX.MetricsFailures? GetMetricsFailures(string viewName, string mapName, string id, Ice.Current c)
        {
            lock (this)
            {
                MetricsViewI? view = getMetricsView(viewName);
                if (view != null)
                {
                    return view.getFailures(mapName, id);
                }
                return new IceMX.MetricsFailures();
            }
        }

        public void registerMap<T>(string map, Action updater)
            where T : IceMX.Metrics, new()
        {
            bool updated;
            MetricsMapFactory<T> factory;
            lock (this)
            {
                factory = new MetricsMapFactory<T>(updater);
                _factories.Add(map, factory);
                updated = addOrUpdateMap(map, factory);
            }
            if (updated)
            {
                factory.update();
            }
        }

        public void registerSubMap<S>(string map, string subMap, System.Reflection.FieldInfo field)
            where S : IceMX.Metrics, new()
        {
            bool updated;
            IMetricsMapFactory factory;
            lock (this)
            {
                if (!_factories.TryGetValue(map, out factory))
                {
                    return;
                }
                factory.registerSubMap<S>(subMap, field);
                removeMap(map);
                updated = addOrUpdateMap(map, factory);
            }
            if (updated)
            {
                factory.update();
            }
        }

        public void unregisterMap(string mapName)
        {
            bool updated;
            IMetricsMapFactory factory;
            lock (this)
            {
                if (!_factories.TryGetValue(mapName, out factory))
                {
                    return;
                }
                _factories.Remove(mapName);
                updated = removeMap(mapName);
            }
            if (updated)
            {
                factory.update();
            }
        }

        public List<MetricsMap<T>> getMaps<T>(string mapName) where T : IceMX.Metrics, new()
        {
            List<MetricsMap<T>> maps = new List<MetricsMap<T>>();
            foreach (MetricsViewI v in _views.Values)
            {
                MetricsMap<T>? map = v.getMap<T>(mapName);
                if (map != null)
                {
                    maps.Add(map);
                }
            }
            return maps;
        }

        public Ice.Logger getLogger()
        {
            return _logger;
        }

        public void updated(Dictionary<string, string> props)
        {
            foreach (KeyValuePair<string, string> e in props)
            {
                if (e.Key.IndexOf("IceMX.") == 0)
                {
                    // Udpate the metrics views using the new configuration.
                    try
                    {
                        updateViews();
                    }
                    catch (Exception ex)
                    {
                        _logger.warning("unexpected exception while updating metrics view configuration:\n" +
                                        ex.ToString());
                    }
                    return;
                }
            }
        }

        private MetricsViewI? getMetricsView(string name)
        {
            MetricsViewI view;
            if (!_views.TryGetValue(name, out view))
            {
                if (!_disabledViews.Contains(name))
                {
                    throw new IceMX.UnknownMetricsView();
                }
                return null;
            }
            return view;
        }

        private bool addOrUpdateMap(string mapName, IMetricsMapFactory factory)
        {
            bool updated = false;
            foreach (MetricsViewI v in _views.Values)
            {
                updated |= v.addOrUpdateMap(_communicator, mapName, factory, _logger);
            }
            return updated;
        }

        private bool removeMap(string mapName)
        {
            bool updated = false;
            foreach (MetricsViewI v in _views.Values)
            {
                updated |= v.removeMap(mapName);
            }
            return updated;
        }

        private readonly Ice.Communicator _communicator;
        private readonly Ice.Logger _logger;
        private readonly Dictionary<string, IMetricsMapFactory> _factories =
            new Dictionary<string, IMetricsMapFactory>();
        private Dictionary<string, MetricsViewI> _views = new Dictionary<string, MetricsViewI>();
        private List<string> _disabledViews = new List<string>();
    }
}
