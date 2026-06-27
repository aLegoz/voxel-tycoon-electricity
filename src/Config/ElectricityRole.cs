namespace TVVTM.Electricity.Config
{
    /// <summary>
    /// Role an asset plays in the electricity network. Serialized by name
    /// (Voxel Tycoon's JsonHelper registers a StringEnumConverter).
    /// </summary>
    public enum ElectricityRole
    {
        /// <summary>Draws power from its grid; freezes when the grid is under-powered.</summary>
        Consumer,

        /// <summary>Feeds power into its grid while fueled (coal in its Device input buffer).</summary>
        Generator,

        /// <summary>
        /// Sells power to its grid for money (e.g. a city heating plant). Always available while
        /// connected; the company is billed per watt actually drawn from it. No fuel needed.
        /// </summary>
        Seller,

        /// <summary>
        /// Not a building spec — carries mod-wide default values (no TargetUri required).
        /// Lets defaults stay data-driven through the asset pipeline. The last one loaded wins.
        /// </summary>
        Defaults,
    }
}
