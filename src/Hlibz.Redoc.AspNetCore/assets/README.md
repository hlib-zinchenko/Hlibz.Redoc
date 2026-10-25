# Vendored ReDoc bundle

`redoc.standalone.js.gz` is the ReDoc community-edition bundle this package embeds and serves. It
is checked in rather than fetched during the build: the build stays offline and deterministic, and
the file changes only when ReDoc is upgraded. `Swashbuckle.AspNetCore.ReDoc` embeds its copy the
same way.

It is stored gzipped because that is also how it is served — the bytes go out with
`Content-Encoding: gzip` and are never decompressed on the server for a client that accepts gzip,
which every browser does.

## Current version

| | |
|---|---|
| ReDoc | **3.0.1** |
| npm tarball | `https://registry.npmjs.org/redoc/-/redoc-3.0.1.tgz` |
| tarball integrity | `sha512-KDfoKaYW19LCl4YStgBnwlaNjYEFfs5zCNt1s1WZ2YM5u8d26Yuzd2l43fZO851ZsgIMe/D640fDJkD8eaRQKQ==` |
| file in tarball | `package/bundles/redoc.standalone.js` |
| that file, sha256 | `8c70c2de83d33e33a75b225e573d6d9fe851f066f824b44b1637d831d52d7543` |
| uncompressed | 2,290,276 bytes |
| as stored | 648,868 bytes |

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
cp /tmp/redoc/package/LICENSE src/Hlibz.Redoc.AspNetCore/assets/redoc-LICENSE.txt
```

`gzip -n` matters: without it the gzip header carries a timestamp and the file differs on every
run even when the bundle hasn't changed.

Then update `ReDocBundle.Version` and this table, and re-check the two things the bundle can break:
the page still renders (run `samples/Sample.Own`), and the CSP in the README still covers whatever
the new bundle fetches.
