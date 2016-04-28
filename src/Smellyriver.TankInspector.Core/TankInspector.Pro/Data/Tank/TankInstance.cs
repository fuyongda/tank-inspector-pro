﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Smellyriver.TankInspector.Pro.Data.Tank.Scripting;
using Smellyriver.TankInspector.Pro.Repository;
using Smellyriver.Utilities;
using TankEntity = Smellyriver.TankInspector.Pro.Data.Entities.Tank;

namespace Smellyriver.TankInspector.Pro.Data.Tank
{
    public class TankInstance : XQueryable, ICloneable
    {
       

        public event EventHandler<BasicConfigurationChangedEventArgs> BasicConfigurationChanged;


        private readonly IRepository _repository;

        public IRepository Repository
        {
            get { return _repository; }
        }

        private readonly TankEntity _tank;

        public TankEntity Tank
        {
            get { return _tank; }
        }

        private TankConfiguration _tankConfiguration;
        public TankConfiguration TankConfiguration
        {
            get { return _tankConfiguration; }
            set
            {
                this.MigrateTankConfiguration(_tankConfiguration, value);
                _tankConfiguration = value;
            }
        }

        private CrewConfiguration _crewConfiguration;
        public CrewConfiguration CrewConfiguration
        {
            get { return _crewConfiguration; }
            set
            {
                this.MigrateCrewConfiguration(_crewConfiguration, value);
                _crewConfiguration = value;
            }
        }

        private CustomizationConfiguration _customizationConfiguration;

        public CustomizationConfiguration CustomizationConfiguration
        {
            get { return _customizationConfiguration; }
            set
            {
                this.MigrateCustomizationConfiguration(_customizationConfiguration, value);
                _customizationConfiguration = value;
            }
        }

        public TankTransform Transform
        {
            get { return _tankInstanceConfigurationInfo.Transform; }
            set { _tankInstanceConfigurationInfo.Transform = value; }
        }

        private readonly TankInstanceConfigurationInfo _tankInstanceConfigurationInfo;

        public TankInstanceConfigurationInfo ConfigurationInfo
        {
            get { return _tankInstanceConfigurationInfo; }
        }

        private readonly ScriptHost _scriptHost;

        public TankInstance(IRepository repository, TankEntity tank, TankInstanceConfigurationInfo configInfo)
            : base(tank.ToElement())
        {
            this._repository = repository;
            this._tank = tank;

            this.Element.Name = "data";
            this.Element.Element("crews").Remove();
            this.Element.Element("chassis").Remove();
            this.Element.Element("turrets").Remove();
            this.Element.Element("engines").Remove();
            this.Element.Element("radios").Remove();
            var fuelTank = new XElement(this.Element.Element("fuelTanks").Element("fuelTank"));
            this.Element.Element("fuelTanks").ReplaceWith(fuelTank);

            _scriptHost = new ScriptHost();
            this.TankConfiguration = new TankConfiguration(repository,
                                                           tank,
                                                           _scriptHost,
                                                           configInfo == null
                                                               ? null
                                                               : configInfo.TankConfigurationInfo);
            this.CrewConfiguration = new CrewConfiguration(repository,
                                                           tank,
                                                           _scriptHost, configInfo == null
                                                               ? null
                                                               : configInfo.CrewConfigurationInfo);
            this.CustomizationConfiguration = new CustomizationConfiguration(repository,
                                                                             tank,
                                                                             _scriptHost,
                                                                             configInfo == null
                                                                                 ? null
                                                                                 : configInfo.CustomizationConfigurationInfo);

            this.Element.Add(_scriptHost.Element);
            _scriptHost.ElementChanged += OnSubElementChanged;

            if (configInfo == null)
            {
                _tankInstanceConfigurationInfo = new TankInstanceConfigurationInfo();
                _tankInstanceConfigurationInfo.TankConfigurationInfo = this.TankConfiguration.TankConfigurationInfo;
                _tankInstanceConfigurationInfo.CrewConfigurationInfo = this.CrewConfiguration.CrewConfigurationInfo;
                _tankInstanceConfigurationInfo.CustomizationConfigurationInfo = this.CustomizationConfiguration.CustomizationConfigurationInfo;
            }
            else
            {
                _tankInstanceConfigurationInfo = configInfo;
            }
        }


        private void MigrateTankConfiguration(TankConfiguration oldConfiguration, TankConfiguration newConfiguration)
        {
            if (oldConfiguration != null)
            {
                oldConfiguration.ModuleElementChanged -= OnSubElementChanged;
                oldConfiguration.EquipmentElementChanged -= OnAccessoryElementChanged;
                oldConfiguration.ConsumableElementChanged -= OnAccessoryElementChanged;
            }

            newConfiguration.ModuleElementChanged += OnSubElementChanged;
            newConfiguration.EquipmentElementChanged += OnAccessoryElementChanged;
            newConfiguration.ConsumableElementChanged += OnAccessoryElementChanged;

            if (oldConfiguration == null)
            {
                this.Element.Add(newConfiguration.GunElement);
                this.Element.Add(newConfiguration.TurretElement);
                this.Element.Add(newConfiguration.ChassisElement);
                this.Element.Add(newConfiguration.EngineElement);
                this.Element.Add(newConfiguration.RadioElement);
                this.Element.Add(newConfiguration.AmmunitionElement);
                this.Element.Add(newConfiguration.EquipmentsElement);
                this.Element.Add(newConfiguration.ConsumablesElement);
            }
            else
            {
                oldConfiguration.GunElement.ReplaceWith(newConfiguration.GunElement);
                oldConfiguration.TurretElement.ReplaceWith(newConfiguration.TurretElement);
                oldConfiguration.ChassisElement.ReplaceWith(newConfiguration.ChassisElement);
                oldConfiguration.EngineElement.ReplaceWith(newConfiguration.EngineElement);
                oldConfiguration.RadioElement.ReplaceWith(newConfiguration.RadioElement);
                oldConfiguration.AmmunitionElement.ReplaceWith(newConfiguration.AmmunitionElement);
                oldConfiguration.EquipmentsElement.ReplaceWith(newConfiguration.EquipmentsElement);
                oldConfiguration.ConsumablesElement.ReplaceWith(newConfiguration.ConsumablesElement);
            }
        }

        private void MigrateCrewConfiguration(CrewConfiguration oldConfiguration, CrewConfiguration newConfiguration)
        {
            _scriptHost.CrewConfiguration = newConfiguration;
            if (oldConfiguration == null)
            {
                this.Element.Add(newConfiguration.Element);
            }
            else
            {
                oldConfiguration.Element.ReplaceWith(newConfiguration.Element);
            }

        }

        private void MigrateCustomizationConfiguration(CustomizationConfiguration oldConfiguration, CustomizationConfiguration newConfiguration)
        {
            if (oldConfiguration != null)
            {
                if (oldConfiguration.CamouflageElement != null)
                    oldConfiguration.CamouflageElement.Remove();

                if (oldConfiguration.InscriptionElement != null)
                    oldConfiguration.InscriptionElement.Remove();

                newConfiguration.InscriptionElementChanged -= OnInscriptionElementChanged;
                newConfiguration.CamouflageElementChanged -= OnCamouflageElementChanged;
            }


            if (newConfiguration.CamouflageElement != null)
                this.Element.Add(newConfiguration.CamouflageElement);

            if (newConfiguration.InscriptionElement != null)
                this.Element.Add(newConfiguration.InscriptionElement);

            newConfiguration.InscriptionElementChanged += OnInscriptionElementChanged;
            newConfiguration.CamouflageElementChanged += OnCamouflageElementChanged;
        }

        void OnCamouflageElementChanged(object sender, EventArgs e)
        {
            if (this.CustomizationConfiguration.CamouflageElement == null)
            {
                var existedElement = this.Element.Element(CustomizationConfiguration.CamouflageElementName);
                if (existedElement != null)
                    existedElement.Remove();
            }
            else
            {
                var camouflageElement = new XElement(this.Element.Element("camouflageInfo"));
                foreach (var element in this.CustomizationConfiguration.CamouflageElement.Elements())
                    camouflageElement.AddOrReplace(element);
                foreach (var attribute in this.CustomizationConfiguration.CamouflageElement.Attributes())
                    camouflageElement.SetAttributeValue(attribute.Name, attribute.Value);
                camouflageElement.Name = CustomizationConfiguration.CamouflageElementName;
                this.Element.AddOrReplace(camouflageElement);
            }
        }

        void OnInscriptionElementChanged(object sender, EventArgs e)
        {
            if (this.CustomizationConfiguration.InscriptionElement == null)
            {
                var existedElement = this.Element.Element(CustomizationConfiguration.InscriptionElementName);
                if (existedElement != null)
                    existedElement.Remove();
            }
            else
            this.Element.AddOrReplace(this.CustomizationConfiguration.InscriptionElement);
        }


        void OnAccessoryElementChanged(object sender, ElementChangedEventArgs e)
        {
            this.OnBasicConfigurationChanged();
        }

        void OnSubElementChanged(object sender, ElementChangedEventArgs e)
        {
            this.ReplaceSubElement(e.Element);
            this.OnBasicConfigurationChanged();
        }

        private void ReplaceSubElement(XElement element)
        {
            var existedElement = this.Element.Element(element.Name);
            if (existedElement != null)
                existedElement.ReplaceWith(element);
            else
                this.Element.Add(element);
        }

        private void OnBasicConfigurationChanged()
        {
            if (this.BasicConfigurationChanged != null)
                this.BasicConfigurationChanged(this, new BasicConfigurationChangedEventArgs(BasicConfigurationAspect.Dummy));
        }

        public double GetThinnestArmor(bool spaced)
        {
            return MathEx.Min(this.Tank.Hull.GetThinnestArmor(spaced),
                              this.TankConfiguration.Gun.GetThinnestArmor(spaced),
                              this.TankConfiguration.Turret.GetThinnestArmor(spaced),
                              this.TankConfiguration.Chassis.GetThinnestArmor(spaced));
        }

        public double GetThickestArmor(bool spaced)
        {
            return MathEx.Max(this.Tank.Hull.GetThickestArmor(spaced),
                              this.TankConfiguration.Gun.GetThickestArmor(spaced),
                              this.TankConfiguration.Turret.GetThickestArmor(spaced),
                              this.TankConfiguration.Chassis.GetThickestArmor(spaced));
        }

        public double[] GetArmorValues(bool spaced)
        {
            return this.Tank.Hull.GetArmorValues(spaced)
                   .Union(this.TankConfiguration.Gun.GetArmorValues(spaced))
                   .Union(this.TankConfiguration.Turret.GetArmorValues(spaced))
                   .Union(this.TankConfiguration.Chassis.GetArmorValues(spaced))
                   .Distinct()
                   .ToArray();
        }

        public TankInstance Clone()
        {
            return new TankInstance(this.Repository, this.Tank, _tankInstanceConfigurationInfo.Clone());
        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }
    }
}