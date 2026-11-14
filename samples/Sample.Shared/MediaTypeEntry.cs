// Microsoft.OpenApi 3.x, which Microsoft.AspNetCore.OpenApi 11 brings, types a content map's
// entries as the interface where 2.x had the class. The transformers read an entry through this
// name and set what they need on it as the class, so the same source builds on both.
#if NET11_0_OR_GREATER
global using MediaTypeEntry = Microsoft.OpenApi.IOpenApiMediaType;
#else
global using MediaTypeEntry = Microsoft.OpenApi.OpenApiMediaType;
#endif
