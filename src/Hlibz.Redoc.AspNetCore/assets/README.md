# Vendored ReDoc bundle

`redoc.standalone.js.gz` and `redoc.standalone.js.br` are the ReDoc community-edition bundle this
package embeds and serves. They are checked in rather than fetched during the build: the build
stays offline and deterministic, and the files change only when ReDoc is upgraded.
`Swashbuckle.AspNetCore.ReDoc` embeds its copy the same way, gzip only.

They are stored compressed because that is also how they are served — the bytes go out with
`Content-Encoding: br` or `gzip` and are never compressed or decompressed per request. Both
encodings are pre-built at quality 11, which only makes sense because this file is the same bytes
for the life of a package version; a response-compression middleware would pay a much cheaper
setting on every cache miss instead. Brotli is preferred when the request accepts it, and the
decompressed bytes are produced on demand for a client that accepts neither.

**Regenerate both together.** A test decompresses each and compares them, because a brotli copy of
one version next to a gzip copy of another would show up only as "it works in one browser".

## Current version

| | |
|---|---|
| ReDoc | **3.0.1** |
| npm tarball | `https://registry.npmjs.org/redoc/-/redoc-3.0.1.tgz` |
| tarball integrity | `sha512-KDfoKaYW19LCl4YStgBnwlaNjYEFfs5zCNt1s1WZ2YM5u8d26Yuzd2l43fZO851ZsgIMe/D640fDJkD8eaRQKQ==` |
| file in tarball | `package/bundles/redoc.standalone.js` |
| that file, sha256 | `8c70c2de83d33e33a75b225e573d6d9fe851f066f824b44b1637d831d52d7543` |
| uncompressed | 2,290,276 bytes |
| as stored, gzip | 648,868 bytes |
| as stored, brotli | 519,455 bytes |

`redoc-LICENSE.txt` is ReDoc's own MIT licence, taken from the same tarball and packed alongside
the bundle. `ReDocBundle.Version` must match the version above — a test asserts it.

## Upgrading

No Node, no `npm`. From the repository root:

```bash
curl -sL https://registry.npmjs.org/redoc/-/redoc-<version>.tgz -o /tmp/redoc.tgz
shasum -a 512 /tmp/redoc.tgz   # compare against the integrity published by the registry
mkdir -p /tmp/redoc && tar -xzf /tmp/redoc.tgz -C /tmp/redoc
gzip -9 -n -c /tmp/redoc/package/bundles/redoc.standalone.js \
  > src/Hlibz.Redoc.AspNetCore/assets/redoc.standalone.js.gz
brotli -q 11 -f -c /tmp/redoc/package/bundles/redoc.standalone.js \
  > src/Hlibz.Redoc.AspNetCore/assets/redoc.standalone.js.br
cp /tmp/redoc/package/LICENSE src/Hlibz.Redoc.AspNetCore/assets/redoc-LICENSE.txt
```

`gzip -n` matters: without it the gzip header carries a timestamp and the file differs on every
run even when the bundle hasn't changed. Brotli stores no timestamp, so `brotli` needs no such
flag.

Then update `ReDocBundle.Version` and this table, and re-check the two things the bundle can break:
the page still renders (run `samples/Sample.Own`), and the CSP in the README still covers whatever
the new bundle fetches.
