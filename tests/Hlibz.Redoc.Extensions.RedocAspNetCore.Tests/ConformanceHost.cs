// The whole of this suite is the shared conformance source in ../Conformance, compiled against
// this adapter. Those files name `ReDocOptions`, `ReDocDocumentsOptions`, `UseReDocDocuments` and
// `UseDarkTheme` unqualified; everything but the host's own options type already resolves, because
// both adapters put their public surface in Hlibz.Redoc.Extensions under identical names. This
// alias supplies the one piece that differs.
global using ReDocOptions = Redoc.AspNetCore.ReDocOptions;
