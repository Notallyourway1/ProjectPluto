namespace ProjectPluto
{
    // Tidies the right-click menu: the stock modules' rows (three control surfaces, two engines, intake,
    // gimbal, antenna and so on) go into one collapsed group, below the Pluto groups. Nothing is removed,
    // except the ramjet's thrust limiter, which the reactor overrides every frame anyway.
    public sealed class ModulePlutoMenu : PartModule
    {
        [KSPField] public string ramjetEngineID = "Ramjet";

        private static readonly BasePAWGroup StockGroup =
            new BasePAWGroup(PlutoReadout.StockGroup, PlutoReadout.StockGroupTitle, true);

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            foreach (PartModule module in part.Modules)
            {
                if (module == null || module.GetType().Namespace == typeof(ModulePlutoMenu).Namespace)
                    continue;
                foreach (BaseField field in module.Fields)
                {
                    if (field.group == null || string.IsNullOrEmpty(field.group.name))
                        field.group = StockGroup;
                }
                foreach (BaseEvent e in module.Events)
                {
                    if (e.group == null || string.IsNullOrEmpty(e.group.name))
                        e.group = StockGroup;
                }
                if (module is ModuleEngines engine && engine.engineID == ramjetEngineID)
                {
                    BaseField limiter = engine.Fields[nameof(ModuleEngines.thrustPercentage)];
                    if (limiter != null)
                        limiter.guiActive = limiter.guiActiveEditor = false;
                }
            }
        }
    }
}
