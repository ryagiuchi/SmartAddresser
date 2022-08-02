using System;
using System.Linq;
using System.Reflection;
using SmartAddresser.Editor.Core.Models.EntryRules.AddressRules;
using SmartAddresser.Editor.Foundation.CustomDrawers;
using SmartAddresser.Editor.Foundation.TinyRx;
using SmartAddresser.Editor.Foundation.TinyRx.ObservableProperty;
using UnityEditor;
using UnityEngine;

namespace SmartAddresser.Editor.Core.Tools.Addresser.LayoutRuleEditor
{
    /// <summary>
    ///     View for the right panel of the Addresses tab of the Address Rule Editor.
    /// </summary>
    internal sealed class AddressProviderPanelView : IDisposable
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly Subject<Empty> _mouseButtonClickedSubject = new Subject<Empty>();
        private readonly string[] _providerNames;
        private readonly Subject<Type> _providerTypeChangedSubject = new Subject<Type>();
        private readonly Type[] _providerTypes;
        private readonly Subject<Empty> _providerValueChangedSubject = new Subject<Empty>();

        private ICustomDrawer _drawer;
        private int _selectedIndex;

        public AddressProviderPanelView(IReadOnlyObservableProperty<IAddressProvider> provider)
        {
            TypeCache.GetTypesDerivedFrom<IAddressProvider>();

            var providerType = provider.Value.GetType();
            var types = TypeCache.GetTypesDerivedFrom<IAddressProvider>()
                .Where(x => !x.IsAbstract)
                .Where(x => x.GetCustomAttribute<IgnoreAddressProviderAttribute>() == null)
                .ToArray();

            _providerTypes = new Type[types.Length];
            _providerNames = new string[types.Length];
            for (var index = 0; index < types.Length; index++)
            {
                var type = types[index];
                _providerTypes[index] = type;
                _providerNames[index] = type.Name;
                if (providerType == type)
                    _selectedIndex = index;
            }

            provider.Subscribe(SetProvider).DisposeWith(_disposables);
        }

        public IObservable<Empty> ProviderValueChangedAsObservable => _providerValueChangedSubject;

        public IObservable<Type> ProviderTypeChangedAsObservable => _providerTypeChangedSubject;

        public IObservable<Empty> MouseButtonClickedAsObservable => _mouseButtonClickedSubject;

        public void Dispose()
        {
            _disposables.Dispose();
            _providerValueChangedSubject.Dispose();
            _providerTypeChangedSubject.Dispose();
            _mouseButtonClickedSubject.Dispose();
        }

        public void DoLayout()
        {
            // Click
            if (Event.current.type == EventType.MouseDown)
                _mouseButtonClickedSubject.OnNext(Empty.Default);

            // Provider Selector
            using (var ccs = new EditorGUI.ChangeCheckScope())
            {
                _selectedIndex = EditorGUILayout.Popup("Provider", _selectedIndex, _providerNames);

                if (ccs.changed)
                {
                    var type = _providerTypes[_selectedIndex];
                    _providerTypeChangedSubject.OnNext(type);
                }
            }

            if (_drawer == null)
                return;

            // Provider Drawer
            using (var ccs = new EditorGUI.ChangeCheckScope())
            {
                _drawer.DoLayout();

                // Notify when any value is changed.
                if (ccs.changed)
                    _providerValueChangedSubject.OnNext(Empty.Default);
            }
        }

        private void SetProvider(IAddressProvider provider)
        {
            if (provider == null)
            {
                _drawer = null;
                return;
            }

            var drawer = CustomDrawerFactory.Create(provider.GetType());
            if (drawer == null)
            {
                Debug.LogError($"Drawer of {provider.GetType().Name} is not found.");
                _drawer = null;
                return;
            }

            drawer.Setup(provider);
            _drawer = drawer;
            _selectedIndex = _providerTypes.ToList().IndexOf(provider.GetType());
        }
    }
}
