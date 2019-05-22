using System.Resources;
using System.Reflection;
using System.Runtime.InteropServices;

#if x64
[assembly: AssemblyTitle("Si13n7 Dev.™ Portalizer Library (64-bit)")]
[assembly: AssemblyDescription("Si13n7 Dev.™ Portalizer Library compiled for 64-bit platform environments")]
[assembly: AssemblyProduct("SilDev.PortalizerLib64")]
#else
[assembly: AssemblyTitle("Si13n7 Dev.™ Portalizer Library")]
[assembly: AssemblyDescription("Si13n7 Dev.™ Portalizer Library compiled for 32-bit platform environments")]
[assembly: AssemblyProduct("SilDev.PortalizerLib")]
#endif

#if debug
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyCompany("Si13n7 Dev.™")]
[assembly: AssemblyCopyright("Copyright © Si13n7 Dev.™ 2019")]
[assembly: AssemblyTrademark("Si13n7 Dev.™")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("8d46b6df-a7d2-4093-a4be-30d2a1bc38a1")]

[assembly: AssemblyVersion("19.5.22.0")]

[assembly: NeutralResourcesLanguage("")]
