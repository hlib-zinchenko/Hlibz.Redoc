// Try-it console for ReDoc 3, served by Hlibz.Redoc.AspNetCore when EnableTryIt() is on.
//
// It never touches ReDoc's React state. It reads the spec itself, mounts a "Try it" button into
// each operation's page-actions, and opens a <dialog> of its own. The DOM contract it relies on
// is ReDoc 3's: [data-item-kind="httpItem"][data-section-id] per operation, whose id ends in
// its lowercased operationId when it has one and in /paths/{JSON-pointer path}/{method} when it
// doesn't, and [data-testid="page-actions"] inside it. Measured on 3.0.1; TRY-IT.md has the
// details.
//
// Auth schemes and request bodies are handled through two small registries (auth, bodies), which
// is how other schemes and media types get added without touching anything else.
//
// Everything that reaches the page goes through textContent or setAttribute, never innerHTML:
// the spec and the response are both data, and the response is attacker-influenced.

(function () {
  'use strict';

  var self = document.querySelector('script[data-hlibz-tryit]');
  if (!self) {
    return;
  }

  var config = {
    specUrl: self.getAttribute('data-spec-url'),
    appBase: new URL(self.getAttribute('data-app-base') || './', location.href).href,
    storage: self.getAttribute('data-credential-storage'),
    // Seconds stored credentials last after they were last typed; absent means until forgotten.
    lifetime: Number(self.getAttribute('data-credential-lifetime')) || 0,
    cookies: self.getAttribute('data-send-cookies') !== 'false',
    safeOnly: self.getAttribute('data-safe-methods-only') === 'true',
  };

  var METHODS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace', 'query'];
  // Safe in RFC 9110's sense (QUERY by its draft): a request that asks, never one that changes.
  var SAFE_METHODS = ['get', 'head', 'options', 'trace', 'query'];
  // One key for the whole app, not one per page, so what a viewer typed on one document is there
  // on every other document the picker leads to.
  var STORAGE_KEY = 'hlibz.tryit.credentials:' + new URL(config.appBase).pathname;

  // ---------------------------------------------------------------------------------------------
  // Small helpers

  function h(tag, attrs) {
    var node = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(function (key) {
        var value = attrs[key];
        if (value === undefined || value === null || value === false) {
          return;
        }
        if (key === 'text') {
          node.textContent = value;
        } else if (key.slice(0, 2) === 'on') {
          node.addEventListener(key.slice(2), value);
        } else {
          node.setAttribute(key, value === true ? '' : String(value));
        }
      });
    }
    for (var i = 2; i < arguments.length; i++) {
      var child = arguments[i];
      if (child === null || child === undefined || child === false) {
        continue;
      }
      (Array.isArray(child) ? child : [child]).forEach(function (c) {
        if (c !== null && c !== undefined && c !== false) {
          node.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
        }
      });
    }
    return node;
  }

  function unescapePointer(segment) {
    return segment.replace(/~1/g, '/').replace(/~0/g, '~');
  }

  // The section id ReDoc gives an operation: /{tag-slug}/paths/{pointer}/{method}. The pointer
  // has every '/' escaped, so the last '/paths/' is the one that starts it.
  function parseSectionId(id) {
    var at = id.lastIndexOf('/paths/');
    if (at < 0) {
      return null;
    }
    var rest = id.slice(at + '/paths/'.length);
    var cut = rest.lastIndexOf('/');
    if (cut < 0) {
      return null;
    }
    var method = rest.slice(cut + 1).toLowerCase();
    if (METHODS.indexOf(method) < 0) {
      return null;
    }
    var pointer = rest.slice(0, cut);
    try {
      pointer = decodeURIComponent(pointer);
    } catch (e) { /* already decoded */ }
    return { path: unescapePointer(pointer), method: method };
  }

  // ---------------------------------------------------------------------------------------------
  // The spec, and local $ref resolution

  var spec = null;

  function resolvePointer(ref) {
    if (typeof ref !== 'string' || ref.slice(0, 2) !== '#/') {
      return undefined;
    }
    return ref.slice(2).split('/').reduce(function (node, part) {
      return node == null ? undefined : node[unescapePointer(decodeURIComponent(part))];
    }, spec);
  }

  function deref(node) {
    var guard = 0;
    while (node && typeof node === 'object' && typeof node.$ref === 'string' && guard++ < 32) {
      var target = resolvePointer(node.$ref);
      if (target === undefined) {
        return {};
      }
      node = target;
    }
    return node;
  }

  function operationFor(path, method) {
    var item = spec && spec.paths && deref(spec.paths[path]);
    var op = item && item[method];
    if (!op) {
      return null;
    }

    // Path-level parameters apply unless the operation redefines the same name and location.
    var params = [];
    var seen = {};
    (op.parameters || []).concat(item.parameters || []).forEach(function (p) {
      p = deref(p);
      var key = p.in + ':' + p.name;
      if (!seen[key]) {
        seen[key] = true;
        params.push(p);
      }
    });

    return { path: path, method: method, op: op, params: params };
  }

  // Which operation a section is. ReDoc anchors one with an operationId at
  // /{tag-slug}/{operationId} — lowercased, with '\' written as %5C and nothing else escaped —
  // and only one without at /{tag-slug}/paths/{pointer}/{method}, so the path form is trusted
  // only for an operation that has no operationId.
  var byOperationId = null;

  function operationIdIndex() {
    if (byOperationId) {
      return byOperationId;
    }
    byOperationId = Object.create(null);
    Object.keys((spec && spec.paths) || {}).forEach(function (path) {
      var item = deref(spec.paths[path]);
      METHODS.forEach(function (method) {
        var op = item && item[method];
        if (op && typeof op.operationId === 'string' && op.operationId) {
          var key = op.operationId.replace(/\\/g, '%5C').toLowerCase();
          // Two ids differing only in case share an anchor; neither can be told apart, so
          // neither gets a button rather than one getting the other's.
          byOperationId[key] = key in byOperationId ? null : { path: path, method: method };
        }
      });
    });
    return byOperationId;
  }

  function targetOf(id) {
    var target = parseSectionId(id);
    var entry = target && operationFor(target.path, target.method);
    if (entry && !entry.op.operationId) {
      return target;
    }

    // An operationId may itself contain '/', so the longest one the id ends with wins.
    var index = operationIdIndex();
    var lower = id.toLowerCase();
    var best = null;
    var bestLength = -1;
    Object.keys(index).forEach(function (key) {
      if (index[key] && key.length > bestLength
          && lower.slice(-(key.length + 1)) === '/' + key) {
        best = index[key];
        bestLength = key.length;
      }
    });
    return best;
  }

  // ---------------------------------------------------------------------------------------------
  // Example values from a schema. Deliberately simple: the first of every choice, and readOnly
  // properties left out, since this is a request.

  var FORMAT_EXAMPLES = {
    uuid: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    'date-time': new Date().toISOString(),
    date: new Date().toISOString().slice(0, 10),
    time: '12:00:00',
    email: 'user@example.com',
    uri: 'https://example.com',
    url: 'https://example.com',
    hostname: 'example.com',
    ipv4: '192.0.2.1',
    ipv6: '2001:db8::1',
  };

  function sampleOf(schema, depth, seen) {
    depth = depth || 0;
    seen = seen || [];
    if (!schema || depth > 8) {
      return undefined;
    }
    if (schema.$ref) {
      if (seen.indexOf(schema.$ref) >= 0) {
        return undefined;
      }
      seen = seen.concat(schema.$ref);
      schema = deref(schema);
      if (!schema) {
        return undefined;
      }
    }

    if (schema.example !== undefined) {
      return schema.example;
    }
    if (Array.isArray(schema.examples) && schema.examples.length) {
      return schema.examples[0];
    }
    if (schema.default !== undefined) {
      return schema.default;
    }
    if (schema.const !== undefined) {
      return schema.const;
    }
    if (Array.isArray(schema.enum) && schema.enum.length) {
      return schema.enum[0];
    }

    if (Array.isArray(schema.allOf) && schema.allOf.length) {
      var merged = {};
      var any = false;
      schema.allOf.forEach(function (part) {
        var value = sampleOf(part, depth + 1, seen);
        if (value && typeof value === 'object' && !Array.isArray(value)) {
          Object.assign(merged, value);
          any = true;
        } else if (value !== undefined && !any) {
          merged = value;
        }
      });
      if (schema.properties) {
        Object.assign(merged, sampleOf(
          Object.assign({}, schema, { allOf: undefined }), depth + 1, seen));
      }
      return merged;
    }

    var choice = schema.oneOf || schema.anyOf;
    if (Array.isArray(choice) && choice.length) {
      var notNull = choice.filter(function (c) { return deref(c).type !== 'null'; });
      return sampleOf((notNull.length ? notNull : choice)[0], depth + 1, seen);
    }

    var type = schema.type;
    if (Array.isArray(type)) {
      type = type.filter(function (t) { return t !== 'null'; })[0] || 'null';
    }
    if (!type) {
      type = schema.properties ? 'object' : schema.items ? 'array' : undefined;
    }

    switch (type) {
      case 'object': {
        var result = {};
        var props = schema.properties || {};
        Object.keys(props).forEach(function (name) {
          var prop = deref(props[name]);
          if (prop && prop.readOnly) {
            return;
          }
          var value = sampleOf(props[name], depth + 1, seen);
          if (value !== undefined) {
            result[name] = value;
          }
        });
        return result;
      }
      case 'array': {
        var item = sampleOf(schema.items, depth + 1, seen);
        return item === undefined ? [] : [item];
      }
      case 'string':
        return FORMAT_EXAMPLES[schema.format] || 'string';
      case 'integer':
      case 'number':
        return typeof schema.minimum === 'number' ? schema.minimum : 0;
      case 'boolean':
        return true;
      case 'null':
        return null;
      default:
        return undefined;
    }
  }

  // The named examples a media type or parameter declares that carry a value inline, in order, as
  // { key, label }. One that points at an externalValue is left out: fetching it is another
  // origin's business and another CSP hole.
  function namedExamples(holder) {
    var examples = holder && holder.examples;
    if (!examples || typeof examples !== 'object' || Array.isArray(examples)) {
      return [];
    }
    return Object.keys(examples).filter(function (key) {
      var ex = deref(examples[key]);
      return ex && ex.value !== undefined;
    }).map(function (key) {
      var summary = deref(examples[key]).summary;
      return { key: key, label: summary ? summary + ' (' + key + ')' : key };
    });
  }

  // A body whose schema is a oneOf/anyOf — a polymorphic type, as ASP.NET Core describes one with
  // [JsonDerivedType] — offers each branch as a starting point, when the media type gives no
  // example of its own to start from. Labelled by the discriminator's name for the branch when
  // there is one (and its sample then carries that name in the discriminator property, which the
  // branch's own schema often leaves out), else by the branch's title or schema name.
  function variantsOf(media) {
    if (!media || media.example !== undefined || namedExamples(media).length) {
      return [];
    }
    var schema = deref(media.schema) || {};
    var choice = schema.oneOf || schema.anyOf;
    if (!Array.isArray(choice)) {
      return [];
    }
    var disc = schema.discriminator && typeof schema.discriminator === 'object'
      ? schema.discriminator : {};
    var mapping = disc.mapping || {};
    var list = choice.map(function (branch, i) {
      var target = deref(branch) || {};
      if (target.type === 'null') {
        return null;
      }
      var ref = branch && typeof branch.$ref === 'string' ? branch.$ref : null;
      var schemaName = ref ? unescapePointer(ref.split('/').pop()) : null;
      var tag = ref ? Object.keys(mapping).filter(function (k) { return mapping[k] === ref; })[0]
        : undefined;
      if (!tag && schemaName && disc.propertyName) {
        tag = schemaName;
      }
      return {
        key: 'variant:' + i,
        label: tag || target.title || schemaName || 'Option ' + (i + 1),
        branch: branch,
        tag: disc.propertyName ? tag : undefined,
        property: disc.propertyName,
      };
    }).filter(Boolean);
    return list.length > 1 ? list : [];
  }

  function variantSample(variant) {
    var value = sampleOf(variant.branch);
    if (variant.tag !== undefined && value && typeof value === 'object' && !Array.isArray(value)) {
      // The discriminator first, as a reader expects it, and with the mapped name rather than
      // whatever the branch's schema sampled for it.
      var tagged = {};
      tagged[variant.property] = variant.tag;
      Object.keys(value).forEach(function (k) {
        if (k !== variant.property) {
          tagged[k] = value[k];
        }
      });
      value = tagged;
    }
    return value;
  }

  // What the Example select offers for a media type: its named examples, else its variants.
  function exampleChoices(media) {
    var named = namedExamples(media);
    return named.length ? named : variantsOf(media);
  }

  // The example a media type entry declares — the named one asked for, else its example, else its
  // first named one — before falling back to its schema (through its first variant, if it has
  // variants, so even that one carries its discriminator).
  function mediaExample(media, name) {
    if (!media) {
      return undefined;
    }
    var variants = variantsOf(media);
    var variant = variants.filter(function (v) { return v.key === name; })[0] || variants[0];
    if (variant) {
      return variantSample(variant);
    }
    var named = namedExamples(media);
    var chosen = named.filter(function (n) { return n.key === name; })[0];
    if (chosen) {
      return deref(media.examples[chosen.key]).value;
    }
    if (media.example !== undefined) {
      return media.example;
    }
    if (named.length) {
      return deref(media.examples[named[0].key]).value;
    }
    return sampleOf(media.schema);
  }

  // ---------------------------------------------------------------------------------------------
  // Auth registry. A handler declares the fields it needs and applies their values to a request
  // under construction ({ headers: Headers, query: [[name, value]] }). The first handler whose
  // `matches` accepts a security scheme handles it.

  var authHandlers = [];

  function registerAuth(handler) {
    authHandlers.push(handler);
  }

  function bearer(req, values) {
    if (values.token) {
      req.headers.set('Authorization', 'Bearer ' + values.token.trim());
    }
  }

  registerAuth({
    matches: function (s) { return s.type === 'http' && /^bearer$/i.test(s.scheme || ''); },
    describe: function (s) { return 'Bearer token' + (s.bearerFormat ? ' (' + s.bearerFormat + ')' : ''); },
    fields: [{ name: 'token', label: 'Token', secret: true }],
    apply: bearer,
  });

  registerAuth({
    matches: function (s) { return s.type === 'http' && /^basic$/i.test(s.scheme || ''); },
    describe: function () { return 'HTTP Basic'; },
    fields: [
      { name: 'username', label: 'Username' },
      { name: 'password', label: 'Password', secret: true },
    ],
    apply: function (req, values) {
      if (!values.username && !values.password) {
        return;
      }
      var bytes = new TextEncoder().encode((values.username || '') + ':' + (values.password || ''));
      var binary = '';
      bytes.forEach(function (b) { binary += String.fromCharCode(b); });
      req.headers.set('Authorization', 'Basic ' + btoa(binary));
    },
  });

  registerAuth({
    matches: function (s) { return s.type === 'apiKey' && (s.in === 'header' || s.in === 'query'); },
    describe: function (s) { return 'API key, ' + s.in + ' "' + s.name + '"'; },
    fields: [{ name: 'value', label: 'Value', secret: true }],
    apply: function (req, values, s) {
      if (!values.value) {
        return;
      }
      if (s.in === 'header') {
        req.headers.set(s.name, values.value);
      } else {
        req.query.push([s.name, values.value]);
      }
    },
  });

  // No OAuth2 or OpenID Connect flow yet; what they produce is a bearer token, so the viewer can
  // paste one they already have.
  registerAuth({
    matches: function (s) { return s.type === 'oauth2' || s.type === 'openIdConnect'; },
    describe: function (s) { return (s.type === 'oauth2' ? 'OAuth 2.0' : 'OpenID Connect') + ' — paste an access token'; },
    fields: [{ name: 'token', label: 'Access token', secret: true }],
    apply: bearer,
  });

  function authHandlerFor(scheme) {
    for (var i = 0; i < authHandlers.length; i++) {
      if (authHandlers[i].matches(scheme)) {
        return authHandlers[i];
      }
    }
    return null;
  }

  // Credentials are per security scheme, so one token covers every operation on the page. Kept in
  // memory unless the site chose sessionStorage or localStorage.
  function credentialStore() {
    try {
      return config.storage === 'local' ? localStorage
        : config.storage === 'session' ? sessionStorage
        : null;
    } catch (e) {
      return null; // storage blocked by the browser: memory only
    }
  }

  var credentials = {};
  // Set once a write fails (quota, blocked storage). From then on memory is the only copy, so a
  // read that finds nothing must not wipe it.
  var storageBroken = false;

  // Stored as { expires, credentials }, expires being epoch milliseconds or null. Read again on
  // every open, so another tab's change shows up here: a key saved there is picked up, and one
  // forgotten or expired there is dropped from memory too, so an open page doesn't keep sending it.
  function loadCredentials() {
    var store = credentialStore();
    if (!store || storageBroken) {
      return;
    }
    var saved = null;
    try {
      saved = JSON.parse(store.getItem(STORAGE_KEY) || 'null');
    } catch (e) { /* unreadable: treated as nothing saved */ }
    if (!saved || typeof saved !== 'object' || !saved.credentials
        || typeof saved.credentials !== 'object') {
      credentials = {};
      return;
    }
    if (typeof saved.expires === 'number' && saved.expires <= Date.now()) {
      forgetCredentials();
      return;
    }
    credentials = saved.credentials;
  }

  // Every change restarts the lifetime: it runs from when the viewer last typed.
  function saveCredentials() {
    var store = credentialStore();
    if (!store) {
      return;
    }
    try {
      store.setItem(STORAGE_KEY, JSON.stringify({
        expires: config.lifetime ? Date.now() + config.lifetime * 1000 : null,
        credentials: credentials,
      }));
    } catch (e) {
      storageBroken = true; // storage full or blocked: memory only from here on
    }
  }

  function describeLifetime(seconds) {
    var units = [[86400, 'day'], [3600, 'hour'], [60, 'minute'], [1, 'second']];
    for (var i = 0; i < units.length; i++) {
      if (seconds >= units[i][0] && seconds % units[i][0] === 0) {
        var n = seconds / units[i][0];
        return n + ' ' + units[i][1] + (n === 1 ? '' : 's');
      }
    }
    return seconds + ' seconds';
  }

  function forgetCredentials() {
    credentials = {};
    var store = credentialStore();
    if (store) {
      try { store.removeItem(STORAGE_KEY); } catch (e) { /* nothing to remove */ }
    }
  }

  loadCredentials();

  // Every scheme any of the operation's requirements names. Values are applied for whichever the
  // viewer filled in, which covers both "A or B" and "A and B" without asking them to pick.
  function schemesFor(op) {
    var requirements = op.security || spec.security || [];
    var names = [];
    requirements.forEach(function (req) {
      Object.keys(req || {}).forEach(function (name) {
        if (names.indexOf(name) < 0) {
          names.push(name);
        }
      });
    });
    var defined = (spec.components && spec.components.securitySchemes) || {};
    return names.map(function (name) {
      var scheme = deref(defined[name]) || {};
      return { name: name, scheme: scheme, handler: authHandlerFor(scheme) };
    });
  }

  // ---------------------------------------------------------------------------------------------
  // Body registry. The first handler whose `matches` accepts a media type renders the editor's
  // starting text and turns the edited text into a fetch body, or throws with a message.

  var bodyHandlers = [];

  function registerBody(handler) {
    bodyHandlers.push(handler);
  }

  registerBody({
    matches: function (type) { return /^application\/([\w.-]+\+)?json$/i.test(type); },
    initial: function (media, exampleName) {
      var example = mediaExample(media, exampleName);
      return example === undefined ? '' : JSON.stringify(example, null, 2);
    },
    encode: function (text) {
      if (text.trim() === '') {
        return undefined;
      }
      JSON.parse(text);
      return text;
    },
  });

  // A form body is edited as one name=value per line, unencoded, which reads far better than the
  // encoded wire form. An array becomes one line per item (form style, exploded: OpenAPI's default
  // for a form field); a nested object goes as JSON, which is what most servers that accept one at
  // all expect. Fields named in `skip` (a multipart body's files) are left out.
  function formLines(example, skip) {
    if (typeof example === 'string') {
      // Already in wire form: shown decoded, one field per line.
      return Array.from(new URLSearchParams(example)).map(function (pair) {
        return pair[0] + '=' + pair[1];
      }).join('\n');
    }
    if (!example || typeof example !== 'object' || Array.isArray(example)) {
      return '';
    }
    var lines = [];
    Object.keys(example).forEach(function (name) {
      if (skip && skip.indexOf(name) >= 0) {
        return;
      }
      [].concat(example[name]).forEach(function (value) {
        lines.push(name + '=' + (value !== null && typeof value === 'object'
          ? JSON.stringify(value) : String(value === null ? '' : value)));
      });
    });
    return lines.join('\n');
  }

  // The edited lines back as [name, value] pairs. The name is trimmed, since stray indentation is
  // never meant; the value is sent exactly.
  function parseFormLines(text) {
    var pairs = [];
    text.split(/\r?\n/).forEach(function (line) {
      if (line.trim() === '') {
        return;
      }
      var at = line.indexOf('=');
      var name = (at < 0 ? line : line.slice(0, at)).trim();
      if (!name) {
        throw new Error('"' + line.trim() + '" has no name before its "=".');
      }
      pairs.push([name, at < 0 ? '' : line.slice(at + 1)]);
    });
    return pairs;
  }

  registerBody({
    matches: function (type) { return /^application\/x-www-form-urlencoded$/i.test(type); },
    hint: 'One name=value per line. Values are URL-encoded when sent.',
    initial: function (media, exampleName) {
      return formLines(mediaExample(media, exampleName));
    },
    encode: function (text) {
      var form = new URLSearchParams();
      parseFormLines(text).forEach(function (pair) { form.append(pair[0], pair[1]); });
      var encoded = form.toString();
      return encoded === '' ? undefined : encoded;
    },
  });

  // Every property a form's schema declares, through $ref and allOf (which is how ASP.NET Core
  // describes a file beside other form fields), with whether it is required.
  function formFields(schema, fields, depth) {
    fields = fields || {};
    schema = deref(schema);
    if (!schema || typeof schema !== 'object' || (depth || 0) > 8) {
      return fields;
    }
    var required = Array.isArray(schema.required) ? schema.required : [];
    Object.keys(schema.properties || {}).forEach(function (name) {
      fields[name] = { schema: deref(schema.properties[name]) || {}, required: false };
    });
    (schema.allOf || []).forEach(function (part) { formFields(part, fields, (depth || 0) + 1); });
    required.forEach(function (name) {
      if (fields[name]) {
        fields[name].required = true;
      }
    });
    return fields;
  }

  // A file part is a binary string (3.0) or a string with a contentMediaType (3.1); an array of
  // either is several files under one name.
  function isFileSchema(schema) {
    schema = deref(schema) || {};
    return [].concat(schema.type || []).indexOf('string') >= 0
      && (schema.format === 'binary' || (!!schema.contentMediaType && !schema.contentEncoding));
  }

  function fileFieldsOf(media) {
    var fields = formFields(media && media.schema);
    return Object.keys(fields).map(function (name) {
      var schema = fields[name].schema;
      var many = [].concat(schema.type || []).indexOf('array') >= 0 && isFileSchema(schema.items);
      return many || isFileSchema(schema)
        ? { name: name, multiple: many, required: fields[name].required }
        : null;
    }).filter(Boolean);
  }

  // Text fields are edited like a URL-encoded form's; each file the schema declares gets a picker.
  // The body is FormData, so the browser writes the Content-Type with its boundary itself.
  registerBody({
    matches: function (type) { return /^multipart\/form-data$/i.test(type); },
    hint: 'One name=value per line for the text fields.',
    files: fileFieldsOf,
    initial: function (media, exampleName) {
      var files = fileFieldsOf(media).map(function (f) { return f.name; });
      return formLines(mediaExample(media, exampleName), files);
    },
    encode: function (text, files) {
      var form = new FormData();
      var any = false;
      parseFormLines(text).forEach(function (pair) {
        form.append(pair[0], pair[1]);
        any = true;
      });
      (files || []).forEach(function (f) {
        Array.prototype.forEach.call(f.input.files, function (file) {
          form.append(f.name, file, file.name);
          any = true;
        });
      });
      return any ? form : undefined;
    },
  });

  // A whole-file body: application/octet-stream, image/png, application/pdf and the like. Chosen
  // by the schema when there is one (a binary string, as for a multipart file part), and by the
  // media type when there isn't, since a binary type with no schema can't be typed into a box.
  // The File itself is the fetch body; its Content-Type is the declared one, or, for a wildcard
  // like image/*, the file's own.
  registerBody({
    whole: true,
    matches: function (type, media) {
      var schema = media && media.schema ? deref(media.schema) : null;
      return schema ? isFileSchema(schema) : !isTextual(type);
    },
    hint: 'The chosen file is sent as the whole body.',
    files: function () {
      return [{ name: '', label: 'File', multiple: false, required: false }];
    },
    initial: function () { return ''; },
    encode: function (text, files) {
      var input = files && files[0] && files[0].input;
      return input && input.files.length ? input.files[0] : undefined;
    },
  });

  // Anything else is sent as typed, with its declared Content-Type. Not a promise of support,
  // just not a refusal either.
  var rawBody = {
    raw: true,
    matches: function () { return true; },
    initial: function () { return ''; },
    encode: function (text) { return text === '' ? undefined : text; },
  };

  function bodyHandlerFor(type, media) {
    for (var i = 0; i < bodyHandlers.length; i++) {
      if (bodyHandlers[i].matches(type, media)) {
        return bodyHandlers[i];
      }
    }
    return rawBody;
  }

  // ---------------------------------------------------------------------------------------------
  // Servers

  function serversFor(entry) {
    // The path item through deref, as operationFor reads it: a $ref'd item keeps its servers.
    var declared = entry.op.servers || (deref(spec.paths[entry.path]) || {}).servers
      || spec.servers;
    var list = [];
    (declared || []).forEach(function (server) {
      if (!server || typeof server.url !== 'string') {
        return;
      }
      var names = [];
      server.url.replace(/\{([^}]+)\}/g, function (match, name) {
        if (names.indexOf(name) < 0) {
          names.push(name);
        }
        return match;
      });
      var candidate = {
        template: server.url,
        description: server.description,
        names: names,
        variables: server.variables && typeof server.variables === 'object' ? server.variables : {},
      };
      // A server with no variables has to be a URL as it stands. One with variables may need a
      // value typed before it is one, so it is kept and checked on Send.
      try {
        resolveServer(candidate, {});
      } catch (e) {
        if (!names.length) {
          return;
        }
      }
      list.push(candidate);
    });

    // No servers means "/" in OpenAPI, which on a PathBase or behind a proxy is not where the API
    // is. The page knows where the app base is, so that is the default instead.
    if (!list.length) {
      list.push({ template: config.appBase, description: 'This app', names: [], variables: {} });
    }

    var selected = 0;
    list.some(function (s, i) {
      try {
        if (new URL(resolveServer(s, {})).origin === location.origin) {
          selected = i;
          return true;
        }
      } catch (e) { /* needs a variable typed first */ }
      return false;
    });
    return { list: list, selected: selected };
  }

  // The server's URL with its variables filled in: what was typed, else the variable's default.
  // Values go in as typed, since a variable may stand for a host or several path segments.
  function resolveServer(server, values) {
    var url = server.template.replace(/\{([^}]+)\}/g, function (match, name) {
      var typed = values[name];
      if (typed !== undefined && typed !== '') {
        return typed;
      }
      var variable = deref(server.variables[name]);
      if (variable && variable.default !== undefined && variable.default !== null) {
        return String(variable.default);
      }
      throw new Error('The server variable ' + name + ' needs a value.');
    });
    try {
      return new URL(url, config.appBase).href;
    } catch (e) {
      throw new Error(url + ' is not a server URL this console can send to.');
    }
  }

  // What the server select shows: a plain server's absolute URL, a templated one's template.
  function serverLabel(server) {
    var shown = server.template;
    if (!server.names.length) {
      try {
        shown = resolveServer(server, {});
      } catch (e) { /* listed only if it resolves */ }
    }
    return server.description && server.description !== shown
      ? server.description + ' — ' + shown
      : shown;
  }

  // ---------------------------------------------------------------------------------------------
  // The dialog

  var dialog = null;
  var current = null;
  var inflight = null;

  // What the viewer typed into each operation, and the last response it got, so reopening the same
  // operation picks up where it left off. Memory only, like the default for credentials: a reload
  // starts over. Saved by open() for the panel it replaces, synchronously, rather than on the
  // dialog's close event, which is queued and so isn't guaranteed to land before the next open.
  var drafts = {};

  function draftKey(entry) {
    return entry.method + ' ' + entry.path;
  }

  function paramKey(p) {
    return p.in + ':' + p.name;
  }

  // The response is kept only once it is finished — shown, failed or cancelled — never a
  // "Sending…" that the close aborted.
  function saveDraft(state) {
    var draft = {
      server: state.server.template(),
      serverValues: state.server.values(),
      accept: state.accept ? state.accept.value : undefined,
      headers: state.extraHeaders(),
      params: {},
      body: null,
      response: null,
    };
    state.params.forEach(function (item) {
      draft.params[paramKey(item.param)] = item.input.value;
    });
    if (state.body) {
      draft.body = {
        type: state.body.typeSelect.value,
        example: state.body.exampleSelect.value,
        text: state.body.editor.value,
        files: state.body.files(),
      };
    }
    if (state.responseDone) {
      draft.response = Array.prototype.slice.call(state.response.childNodes);
    }
    drafts[draftKey(state.entry)] = draft;
  }

  function ensureDialog() {
    if (dialog) {
      return dialog;
    }
    dialog = h('dialog', { class: 'hlibz-tryit', 'aria-labelledby': 'hlibz-tryit-title' });

    // ReDoc listens for shortcuts on the document; typing here is not for it.
    dialog.addEventListener('keydown', function (e) {
      if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') {
        e.preventDefault();
        send();
      }
      if (e.key !== 'Escape') {
        e.stopPropagation();
      }
    });
    dialog.addEventListener('close', function () {
      // The event is queued: if the console has been opened again since, this one is stale, and
      // the request and focus it would act on belong to the new panel.
      if (dialog.open) {
        return;
      }
      if (inflight) {
        inflight.abort();
        inflight = null;
      }
      if (current && current.opener && document.contains(current.opener)) {
        current.opener.focus();
      }
    });
    // A click on the backdrop lands on the dialog element itself.
    dialog.addEventListener('click', function (e) {
      if (e.target === dialog) {
        dialog.close();
      }
    });
    document.body.appendChild(dialog);
    return dialog;
  }

  function field(label, input, hint) {
    return h('label', { class: 'hlibz-tryit-field' },
      h('span', { class: 'hlibz-tryit-label' }, label),
      input,
      hint ? h('span', { class: 'hlibz-tryit-hint', text: hint }) : null);
  }

  // A secret is masked until its eye is pressed. The input is labelled through for/id rather than
  // by nesting it in the <label>, so the button's own name doesn't end up in the input's.
  var secretIds = 0;
  var SVG = 'http://www.w3.org/2000/svg';
  var EYE = 'M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z';
  var PUPIL = 'M12 9a3 3 0 1 0 0 6 3 3 0 1 0 0-6z';
  var SLASH = 'M3 3l18 18';

  function icon(paths) {
    var svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    paths.forEach(function (d) {
      var path = document.createElementNS(SVG, 'path');
      path.setAttribute('d', d);
      svg.appendChild(path);
    });
    return svg;
  }

  function secretField(label, input) {
    var id = 'hlibz-tryit-secret-' + (++secretIds);
    input.id = id;
    var reveal = h('button', {
      type: 'button',
      class: 'hlibz-tryit-reveal',
      'aria-controls': id,
      'aria-pressed': 'false',
      'aria-label': 'Show ' + label.toLowerCase(),
      title: 'Show',
      onclick: function () {
        var show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        reveal.setAttribute('aria-pressed', String(show));
        reveal.setAttribute('aria-label', (show ? 'Hide ' : 'Show ') + label.toLowerCase());
        reveal.title = show ? 'Hide' : 'Show';
        reveal.replaceChildren(icon(show ? [EYE, PUPIL, SLASH] : [EYE, PUPIL]));
      },
    }, icon([EYE, PUPIL]));
    return h('div', { class: 'hlibz-tryit-field' },
      h('label', { class: 'hlibz-tryit-label', for: id, text: label }),
      h('div', { class: 'hlibz-tryit-secret' }, input, reveal));
  }

  function paramDefault(p) {
    var named = namedExamples(p);
    var value = p.example !== undefined ? p.example
      : named.length ? deref(p.examples[named[0].key]).value
      : sampleOf(p.schema);
    if (value === undefined || value === null) {
      return '';
    }
    if (Array.isArray(value)) {
      return value.join(',');
    }
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  // The values a single-valued enum or boolean parameter can take, as the strings a <select>
  // offers, or null for anything typed freely. An array of enums stays a comma-separated text box.
  function choicesOf(p) {
    var schema = deref(p.schema) || {};
    var types = [].concat(schema.type || []);
    if (types.indexOf('array') >= 0) {
      return null;
    }
    var values = Array.isArray(schema.enum) ? schema.enum
      : types.indexOf('boolean') >= 0 ? [true, false]
      : null;
    if (!values) {
      return null;
    }
    values = values
      .filter(function (v) { return v !== null && typeof v !== 'object'; })
      .map(String);
    return values.length ? values : null;
  }

  // ---------------------------------------------------------------------------------------------
  // Light checks on what was typed into a parameter, against its schema: type, enum, length,
  // pattern, range, item count and a few formats. Each problem is one sentence; none stops a send.

  var FORMAT_CHECKS = {
    uuid: [/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, 'a UUID'],
    date: [/^\d{4}-\d{2}-\d{2}$/, 'a date like 2024-01-31'],
    'date-time': [/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/i,
      'a date-time like 2024-01-31T12:00:00Z'],
    email: [/^[^\s@]+@[^\s@]+$/, 'an email address'],
  };

  function valueWarnings(schema, text, what) {
    schema = deref(schema) || {};
    var found = [];
    var type = [].concat(schema.type || []).filter(function (t) { return t !== 'null'; })[0];
    if (type === 'integer' || type === 'number') {
      var n = Number(text);
      if (text.trim() === '' || isNaN(n)) {
        return [what + ' should be a number.'];
      }
      if (type === 'integer' && Math.floor(n) !== n) {
        found.push(what + ' should be a whole number.');
      }
      if (typeof schema.minimum === 'number'
          && (schema.exclusiveMinimum === true ? n <= schema.minimum : n < schema.minimum)) {
        found.push(what + ' should be ' + (schema.exclusiveMinimum === true ? 'more than ' : 'at least ')
          + schema.minimum + '.');
      }
      if (typeof schema.exclusiveMinimum === 'number' && n <= schema.exclusiveMinimum) {
        found.push(what + ' should be more than ' + schema.exclusiveMinimum + '.');
      }
      if (typeof schema.maximum === 'number'
          && (schema.exclusiveMaximum === true ? n >= schema.maximum : n > schema.maximum)) {
        found.push(what + ' should be ' + (schema.exclusiveMaximum === true ? 'less than ' : 'at most ')
          + schema.maximum + '.');
      }
      if (typeof schema.exclusiveMaximum === 'number' && n >= schema.exclusiveMaximum) {
        found.push(what + ' should be less than ' + schema.exclusiveMaximum + '.');
      }
    } else if (type === 'boolean') {
      if (text !== 'true' && text !== 'false') {
        found.push(what + ' should be true or false.');
      }
    }
    if (Array.isArray(schema.enum) && schema.enum.length
        && schema.enum.map(String).indexOf(text) < 0) {
      found.push(what + ' should be one of ' + schema.enum.map(String).join(', ') + '.');
    }
    if (typeof schema.minLength === 'number' && text.length < schema.minLength) {
      found.push(what + ' should be at least ' + schema.minLength + ' characters.');
    }
    if (typeof schema.maxLength === 'number' && text.length > schema.maxLength) {
      found.push(what + ' should be at most ' + schema.maxLength + ' characters.');
    }
    if (typeof schema.pattern === 'string') {
      try {
        if (!new RegExp(schema.pattern).test(text)) {
          found.push(what + ' should match ' + schema.pattern + '.');
        }
      } catch (e) { /* a pattern JavaScript can't read is not the viewer's problem */ }
    }
    var format = FORMAT_CHECKS[schema.format];
    if (format && !format[0].test(text)) {
      found.push(what + ' should be ' + format[1] + '.');
    }
    return found;
  }

  function paramWarnings(p, text) {
    if (!p.schema) {
      return [];
    }
    var schema = deref(p.schema) || {};
    var kind = kindOf(schema);
    if (kind === 'object') {
      return []; // paramValue says so on Send if it isn't JSON; deeper checks are the server's.
    }
    if (kind === 'array') {
      var items = text.split(',').map(function (v) { return v.trim(); });
      var found = [];
      if (typeof schema.minItems === 'number' && items.length < schema.minItems) {
        found.push('Give at least ' + schema.minItems + ' values.');
      }
      if (typeof schema.maxItems === 'number' && items.length > schema.maxItems) {
        found.push('Give at most ' + schema.maxItems + ' values.');
      }
      items.forEach(function (item) {
        valueWarnings(schema.items, item, '"' + item + '"').forEach(function (w) {
          if (found.indexOf(w) < 0) {
            found.push(w);
          }
        });
      });
      return found;
    }
    return valueWarnings(schema, text, 'The value');
  }

  // The type, how to type it, and the style it goes out in when that isn't the location's default,
  // since "red,blue" in the box becoming red|blue on the wire would otherwise be a surprise.
  var STYLE_NAMES = {
    label: 'as .a.b (label)',
    matrix: 'as ;name=value (matrix)',
    spaceDelimited: 'space-delimited',
    pipeDelimited: 'pipe-delimited',
    deepObject: 'as name[key]=value',
  };

  // The media types a successful response is documented in (the 2XX ones, else default), in the
  // order the spec gives them. Offered as Accept only when there is a real choice: with one, the
  // server has nothing to negotiate, and narrowing Accept could only earn a 406 on an error.
  function acceptTypesOf(op) {
    var responses = op.responses || {};
    var keys = Object.keys(responses).filter(function (k) { return /^2/.test(k); });
    if (!keys.length && responses['default']) {
      keys = ['default'];
    }
    var types = [];
    keys.forEach(function (k) {
      Object.keys((deref(responses[k]) || {}).content || {}).forEach(function (t) {
        if (types.indexOf(t) < 0 && t.indexOf('*') < 0) {
          types.push(t);
        }
      });
    });
    return types;
  }

  function paramHint(p) {
    if (!p.schema && p.content) {
      return Object.keys(p.content)[0] + ' — sent as typed';
    }
    var schema = deref(p.schema) || {};
    var kind = kindOf(schema);
    var type = [].concat(schema.type || []).filter(function (t) { return t !== 'null'; })[0];
    var parts = [[type || (kind === 'primitive' ? null : kind), schema.format]
      .filter(Boolean).join(', ')];
    if (kind === 'array') {
      parts.push('comma-separated');
    } else if (kind === 'object') {
      parts.push('JSON');
    }
    var s = styleOf(p);
    if ((STYLE_NAMES[s.style] && kind !== 'primitive')
        || s.style === 'label' || s.style === 'matrix') {
      parts.push('sent ' + STYLE_NAMES[s.style]);
    } else if (p.in === 'query' && kind === 'object') {
      parts.push(s.explode ? 'each key sent as its own parameter' : 'sent as one value');
    } else if (p.in === 'query' && kind === 'array' && !s.explode) {
      parts.push('sent as one value');
    }
    return parts.filter(Boolean).join(' — ');
  }

  function open(entry, opener) {
    ensureDialog();
    loadCredentials();
    if (current) {
      saveDraft(current);
    }
    if (inflight) {
      inflight.abort();
      inflight = null;
    }

    var op = entry.op;
    var servers = serversFor(entry);
    var schemes = schemesFor(op);
    var state = { entry: entry, opener: opener, params: [], auth: [], body: null, server: null };
    current = state;
    var draft = drafts[draftKey(entry)] || null;

    // Server
    var serverSelect = h('select', { class: 'hlibz-tryit-input' },
      servers.list.map(function (s, i) {
        return h('option', { value: String(i), selected: i === servers.selected, text: serverLabel(s) });
      }));
    if (draft) {
      servers.list.some(function (s, i) {
        if (s.template === draft.server) {
          serverSelect.value = String(i);
          return true;
        }
        return false;
      });
    }
    // A field per variable of the selected server; what was typed is kept per server template,
    // so switching servers and back doesn't lose it.
    var serverValues = (draft && draft.serverValues) || {};
    var varsBox = h('div', { class: 'hlibz-tryit-server-vars' });
    var varInputs = {};
    var shown = null;
    var keepVars = function () {
      if (shown) {
        var kept = serverValues[shown.template] = {};
        Object.keys(varInputs).forEach(function (name) { kept[name] = varInputs[name].value; });
      }
    };
    var renderVars = function () {
      keepVars();
      shown = servers.list[Number(serverSelect.value)];
      var kept = serverValues[shown.template] || {};
      varInputs = {};
      varsBox.replaceChildren.apply(varsBox, shown.names.map(function (name) {
        var variable = deref(shown.variables[name]) || {};
        var choices = Array.isArray(variable.enum) && variable.enum.length
          ? variable.enum.map(String) : null;
        var input = choices
          ? h('select', { class: 'hlibz-tryit-input' },
            choices.map(function (c) { return h('option', { value: c, text: c }); }))
          : h('input', { class: 'hlibz-tryit-input', type: 'text', spellcheck: 'false' });
        input.value = kept[name] !== undefined ? kept[name]
          : variable.default !== undefined && variable.default !== null ? String(variable.default)
          : '';
        varInputs[name] = input;
        return field(h('span', null, h('code', { text: '{' + name + '}' })), input,
          variable.description ? String(variable.description).split('\n')[0].trim() : null);
      }));
      varsBox.hidden = !shown.names.length;
    };
    serverSelect.addEventListener('change', renderVars);
    renderVars();
    state.server = {
      select: serverSelect,
      template: function () { return shown.template; },
      values: function () {
        keepVars();
        return serverValues;
      },
      url: function () {
        var typed = {};
        Object.keys(varInputs).forEach(function (name) { typed[name] = varInputs[name].value; });
        return resolveServer(shown, typed);
      },
    };

    // Auth
    var authSection = null;
    if (schemes.length) {
      var store = credentialStore();
      var forget = store ? h('div', { class: 'hlibz-tryit-saved' },
        h('span', {
          class: 'hlibz-tryit-hint',
          text: (store === sessionStorage ? 'Saved for this tab' : 'Saved in this browser')
            + (config.lifetime ? ' for ' + describeLifetime(config.lifetime) + ' after the last change' : '')
            + ', on every page of these docs.',
        }),
        h('button', {
          type: 'button',
          class: 'hlibz-tryit-forget',
          text: 'Forget',
          onclick: function () {
            forgetCredentials();
            authSection.querySelectorAll('input[data-scheme]').forEach(function (i) {
              i.value = '';
            });
          },
        })) : null;
      authSection = h('section', { class: 'hlibz-tryit-section' },
        h('h3', { text: 'Authorization' }),
        forget,
        schemes.map(function (s) {
          if (!s.handler) {
            return h('p', { class: 'hlibz-tryit-hint', text: s.name + ': ' + (s.scheme.type || 'unknown') + ' is not supported here.' });
          }
          var values = credentials[s.name] || (credentials[s.name] = {});
          var inputs = s.handler.fields.map(function (f) {
            var input = h('input', {
              class: 'hlibz-tryit-input',
              type: f.secret ? 'password' : 'text',
              autocomplete: 'off',
              spellcheck: 'false',
              value: values[f.name] || '',
              'data-scheme': s.name,
              oninput: function (e) {
                // Through credentials rather than a captured object, which Forget replaces.
                (credentials[s.name] || (credentials[s.name] = {}))[f.name] = e.target.value;
                saveCredentials();
              },
            });
            return f.secret ? secretField(f.label, input) : field(f.label, input);
          });
          state.auth.push(s);
          return h('fieldset', { class: 'hlibz-tryit-scheme' },
            h('legend', { text: s.name + ' · ' + s.handler.describe(s.scheme) }),
            inputs);
        }));
    }

    // Parameters, grouped by where they go. Cookie parameters can't be set from fetch.
    var groups = { path: 'Path', query: 'Query', header: 'Headers' };
    var paramSections = Object.keys(groups).map(function (where) {
      var list = entry.params.filter(function (p) { return p.in === where; });
      if (!list.length) {
        return null;
      }
      return h('section', { class: 'hlibz-tryit-section' },
        h('h3', { text: groups[where] }),
        list.map(function (p) {
          var required = !!p.required || where === 'path';
          var choices = choicesOf(p);
          var input = choices
            ? h('select', { class: 'hlibz-tryit-input', required: required },
              // An optional parameter can be left out, which a select needs a way to say.
              required ? null : h('option', { value: '', text: '— not sent —' }),
              choices.map(function (c) { return h('option', { value: c, text: c }); }))
            : h('input', {
              class: 'hlibz-tryit-input',
              type: 'text',
              spellcheck: 'false',
              required: required,
            });
          input.value = draft && paramKey(p) in draft.params
            ? draft.params[paramKey(p)]
            : paramDefault(p);
          state.params.push({ param: p, input: input });
          var label = h('span', null, h('code', { text: p.name }),
            required ? h('span', { class: 'hlibz-tryit-required', text: ' required' }) : null,
            p.deprecated ? h('span', { class: 'hlibz-tryit-deprecated-tag', text: ' deprecated' }) : null);
          var row = field(label, input, paramHint(p));
          // What the schema says the value should be, said as the viewer types: a warning, never a
          // refusal, because sending a bad value on purpose is half of what a console is for.
          var warning = h('span', { class: 'hlibz-tryit-check', 'aria-live': 'polite', hidden: true });
          var check = function () {
            var problems = input.value === '' ? [] : paramWarnings(p, input.value);
            warning.textContent = problems.join(' ');
            warning.hidden = !problems.length;
          };
          input.addEventListener('input', check);
          input.addEventListener('change', check);
          check();
          row.appendChild(warning);
          return row;
        }));
    });

    // Headers the spec doesn't declare: a correlation id, If-Match, a tenant or version header. One
    // row each, added on demand, and sent after everything else so one can override what the
    // console set itself (Accept, say).
    var headerRows = [];
    var headerList = h('div', { class: 'hlibz-tryit-header-rows' });
    var addHeader = function (name, value, focus) {
      var row = {
        name: h('input', { class: 'hlibz-tryit-input', type: 'text', spellcheck: 'false',
          placeholder: 'Name', 'aria-label': 'Header name', value: name || '' }),
        value: h('input', { class: 'hlibz-tryit-input', type: 'text', spellcheck: 'false',
          placeholder: 'Value', 'aria-label': 'Header value', value: value || '' }),
      };
      row.node = h('div', { class: 'hlibz-tryit-header-row' }, row.name, row.value,
        h('button', {
          type: 'button',
          class: 'hlibz-tryit-ghost',
          'aria-label': 'Remove header',
          title: 'Remove',
          text: '×',
          onclick: function () {
            headerRows.splice(headerRows.indexOf(row), 1);
            row.node.remove();
            addButton.focus();
          },
        }));
      headerRows.push(row);
      headerList.appendChild(row.node);
      if (focus) {
        row.name.focus();
      }
    };
    var addButton = h('button', {
      type: 'button',
      class: 'hlibz-tryit-ghost',
      text: 'Add header',
      onclick: function () { addHeader('', '', true); },
    });
    ((draft && draft.headers) || []).forEach(function (pair) { addHeader(pair[0], pair[1]); });
    state.extraHeaders = function () {
      return headerRows.map(function (row) { return [row.name.value.trim(), row.value.value]; })
        .filter(function (pair) { return pair[0] !== ''; });
    };
    var headersSection = h('section', { class: 'hlibz-tryit-section' },
      h('h3', { text: 'Extra headers' }),
      headerList,
      h('div', { class: 'hlibz-tryit-body-bar' },
        h('span', { class: 'hlibz-tryit-hint',
          text: 'Headers the spec doesn\'t list. A browser won\'t send some, like Cookie or Host.' }),
        h('span', { class: 'hlibz-tryit-push' }, addButton)));

    // Cookie parameters get no field, since a page can't set a request's Cookie header. Named
    // rather than left out, so a request that fails for want of one says why.
    var cookies = entry.params.filter(function (p) { return p.in === 'cookie'; });
    var cookieSection = cookies.length
      ? h('section', { class: 'hlibz-tryit-section' },
        h('h3', { text: 'Cookies' }),
        h('p', { class: 'hlibz-tryit-hint' },
          // h() flattens one level, so the per-cookie pieces are concatenated into one list.
          [].concat.apply([], cookies.map(function (p, i) {
            return [i ? ', ' : null, h('code', { text: p.name }), p.required ? ' (required)' : null];
          })),
          ': a browser doesn\'t let a page set these. '
            + (config.cookies
              ? 'The request carries whatever cookies this browser already holds for the server.'
              : 'This console is set not to send cookies at all.')))
      : null;

    // Body
    var bodySection = null;
    var requestBody = deref(op.requestBody);
    if (requestBody && requestBody.content) {
      var types = Object.keys(requestBody.content);
      var typeSelect = h('select', { class: 'hlibz-tryit-input' },
        types.map(function (t) { return h('option', { value: t, text: t }); }));
      // Prefer a type we actually understand.
      var preferred = types.filter(function (t) {
        return !bodyHandlerFor(t, requestBody.content[t]).raw;
      })[0];
      if (preferred) {
        typeSelect.value = preferred;
      }
      if (draft && draft.body && types.indexOf(draft.body.type) >= 0) {
        typeSelect.value = draft.body.type;
      }
      var editor = h('textarea', { class: 'hlibz-tryit-input hlibz-tryit-code', rows: 12, spellcheck: 'false' });
      var note = h('span', { class: 'hlibz-tryit-hint' });
      // A picker per file the media type's schema declares (multipart only). A picked file can't be
      // put back into a new input, so the inputs themselves are what a draft keeps and reuses.
      var filesBox = h('div', { class: 'hlibz-tryit-files' });
      var fileInputs = [];
      var keptFiles = (draft && draft.body && draft.body.files) || [];
      var renderFiles = function () {
        var handler = bodyHandlerFor(typeSelect.value, requestBody.content[typeSelect.value]);
        var wanted = handler.files ? handler.files(requestBody.content[typeSelect.value]) : [];
        var reusable = keptFiles.concat(fileInputs);
        fileInputs = wanted.map(function (f) {
          var kept = reusable.filter(function (k) {
            return k.name === f.name && k.input.multiple === f.multiple;
          })[0];
          return {
            name: f.name,
            label: f.label,
            required: f.required,
            input: kept ? kept.input : h('input', { type: 'file', class: 'hlibz-tryit-input', multiple: f.multiple }),
          };
        });
        filesBox.replaceChildren.apply(filesBox, fileInputs.map(function (f) {
          return field(
            h('span', null, f.label || h('code', { text: f.name }),
              f.required ? h('span', { class: 'hlibz-tryit-required', text: ' required' }) : null),
            f.input,
            f.input.multiple ? 'One or more files.' : null);
        }));
        filesBox.hidden = !fileInputs.length;
      };
      // Offered only when the media type names more than one example; each type has its own.
      var exampleSelect = h('select', { class: 'hlibz-tryit-input' });
      var exampleField = field('Example', exampleSelect);
      var exampleLabel = exampleField.querySelector('.hlibz-tryit-label');
      var listExamples = function () {
        var media = requestBody.content[typeSelect.value];
        var named = exampleChoices(media);
        exampleLabel.textContent = namedExamples(media).length ? 'Example' : 'Variant';
        exampleSelect.replaceChildren.apply(exampleSelect, named.map(function (n) {
          return h('option', { value: n.key, text: n.label });
        }));
        // A whole-file body has no text to fill in, so no example to pick either.
        exampleField.hidden = named.length < 2
          || !!bodyHandlerFor(typeSelect.value, requestBody.content[typeSelect.value]).whole;
      };
      var fill = function () {
        var handler = bodyHandlerFor(typeSelect.value, requestBody.content[typeSelect.value]);
        editor.value = handler.initial(
          requestBody.content[typeSelect.value], exampleSelect.value || undefined);
        note.textContent = handler.raw
          ? 'Sent as typed: this media type has no editor here yet.'
          : handler.hint || '';
        editor.hidden = !!handler.whole;
        resetButton.hidden = !!handler.whole;
      };
      var resetButton = h('button', {
        type: 'button',
        class: 'hlibz-tryit-ghost hlibz-tryit-push',
        text: 'Reset to example',
        onclick: function () {
          fill();
          editor.focus();
        },
      });
      typeSelect.addEventListener('change', function () {
        listExamples();
        renderFiles();
        fill();
      });
      exampleSelect.addEventListener('change', fill);
      listExamples();
      renderFiles();
      if (draft && draft.body && draft.body.type === typeSelect.value && draft.body.example
          && exampleChoices(requestBody.content[typeSelect.value])
            .some(function (n) { return n.key === draft.body.example; })) {
        exampleSelect.value = draft.body.example;
      }
      fill();
      if (draft && draft.body && draft.body.type === typeSelect.value) {
        editor.value = draft.body.text;
      }
      state.body = {
        typeSelect: typeSelect,
        exampleSelect: exampleSelect,
        editor: editor,
        files: function () { return fileInputs; },
        content: requestBody.content,
        required: !!requestBody.required,
      };
      bodySection = h('section', { class: 'hlibz-tryit-section' },
        h('h3', null, 'Body', requestBody.required ? h('span', { class: 'hlibz-tryit-required', text: ' required' }) : null),
        types.length > 1 ? field('Content type', typeSelect) : h('p', { class: 'hlibz-tryit-hint', text: types[0] }),
        exampleField,
        filesBox,
        editor,
        h('div', { class: 'hlibz-tryit-body-bar' }, note, resetButton));
    }

    // Accept, when the operation can answer in more than one media type.
    var acceptTypes = acceptTypesOf(op);
    var acceptSelect = null;
    if (acceptTypes.length > 1) {
      acceptSelect = h('select', { class: 'hlibz-tryit-input' },
        acceptTypes.map(function (t) { return h('option', { value: t, text: t }); }),
        h('option', { value: '', text: 'Any (no Accept header)' }));
      if (draft && draft.accept !== undefined
          && (draft.accept === '' || acceptTypes.indexOf(draft.accept) >= 0)) {
        acceptSelect.value = draft.accept;
      }
    }
    state.accept = acceptSelect;

    var method = entry.method.toUpperCase();
    var sendButton = h('button', {
      type: 'button',
      class: 'hlibz-tryit-send' + (SAFE_METHODS.indexOf(entry.method) >= 0 ? '' : ' hlibz-tryit-send-unsafe'),
      onclick: send,
      text: 'Send ' + method + ' request',
    });
    state.sendButton = sendButton;
    state.cancelButton = h('button', {
      type: 'button',
      class: 'hlibz-tryit-ghost',
      text: 'Cancel',
      hidden: true,
      onclick: function () {
        if (inflight && current === state) {
          state.cancelled = true;
          inflight.abort();
        }
      },
    });
    // The same request as code to paste elsewhere: a terminal, or a browser's console.
    var copyAs = function (label, render, title) {
      var button = h('button', {
        type: 'button',
        class: 'hlibz-tryit-ghost',
        text: label,
        title: title,
        onclick: function () {
          var req;
          try {
            req = buildRequest(state);
          } catch (e) {
            showError(e.message);
            return;
          }
          copyText(render(entry.method, req)).then(
            function () { flash(button, 'Copied'); },
            function () { flash(button, 'Copy failed'); });
        },
      });
      return button;
    };
    var copyButtons = h('span', { class: 'hlibz-tryit-push hlibz-tryit-copy-as' },
      copyAs('Copy as cURL', curlOf,
        'Includes the credentials typed here. The browser\'s cookies are not included.'),
      copyAs('Copy as fetch', fetchOf,
        'Includes the credentials typed here. Run on this site, it sends the browser\'s cookies too.'));
    state.response = h('div', { class: 'hlibz-tryit-response', 'aria-live': 'polite' });
    if (draft && draft.response) {
      state.response.replaceChildren.apply(state.response, draft.response);
      state.responseDone = true;
    }

    dialog.replaceChildren(
      h('header', { class: 'hlibz-tryit-header' },
        h('span', { class: 'hlibz-tryit-method hlibz-tryit-method-' + entry.method, text: method }),
        h('h2', { id: 'hlibz-tryit-title', text: entry.path }),
        h('button', { type: 'button', class: 'hlibz-tryit-close', 'aria-label': 'Close', text: '×', onclick: function () { dialog.close(); } })),
      h('div', { class: 'hlibz-tryit-body' },
        op.summary ? h('p', { class: 'hlibz-tryit-summary', text: op.summary }) : null,
        op.deprecated === true
          ? h('p', { class: 'hlibz-tryit-undocumented hlibz-tryit-deprecated', role: 'note',
            text: 'Deprecated: this operation still answers, but may be removed. '
              + 'The docs above may say what to use instead.' })
          : null,
        h('section', { class: 'hlibz-tryit-section' },
          field('Server', serverSelect),
          varsBox,
          acceptSelect ? field('Accept', acceptSelect) : null),
        authSection,
        paramSections,
        headersSection,
        cookieSection,
        bodySection,
        h('div', { class: 'hlibz-tryit-actions' },
          sendButton,
          state.cancelButton,
          h('span', { class: 'hlibz-tryit-hint', text: (/Mac|iPhone|iPad/.test(navigator.userAgent) ? '⌘' : 'Ctrl') + '+Enter' }),
          copyButtons),
        state.response));

    if (!dialog.open) {
      dialog.showModal();
    }
    var first = dialog.querySelector('.hlibz-tryit-body input, .hlibz-tryit-body textarea, .hlibz-tryit-body select');
    if (first) {
      first.focus();
    }
  }

  // ---------------------------------------------------------------------------------------------
  // Sending

  function showError(message) {
    current.response.replaceChildren(h('p', { class: 'hlibz-tryit-error', role: 'alert', text: message }));
    current.responseDone = true;
  }

  // ---------------------------------------------------------------------------------------------
  // Parameter serialization, by OpenAPI's style and explode. A parameter is typed into one text box:
  // an array comma-separated, an object as JSON. What goes on the wire is then the parameter's
  // declared style, not the box's.

  // The schema's kind: 'array', 'object' or 'primitive'. A 3.1 type list counts its first non-null
  // member; a schema with no type is judged by what it declares.
  function kindOf(schema) {
    schema = deref(schema) || {};
    var type = [].concat(schema.type || []).filter(function (t) { return t !== 'null'; })[0];
    if (type === 'array' || (!type && schema.items)) {
      return 'array';
    }
    if (type === 'object' || (!type && (schema.properties || schema.additionalProperties))) {
      return 'object';
    }
    return 'primitive';
  }

  // The style a parameter is sent in, with explode resolved to the default for that style: true
  // for form (and deepObject, which has no other form), false for everything else.
  function styleOf(p) {
    var style = p.style
      || (p.in === 'query' || p.in === 'cookie' ? 'form' : 'simple');
    var explode = typeof p.explode === 'boolean' ? p.explode
      : style === 'form' || style === 'deepObject';
    return { style: style, explode: explode };
  }

  // A parameter carried as `content` rather than `schema` is sent exactly as typed.
  function paramValue(p, text) {
    if (!p.schema && p.content) {
      return text;
    }
    var kind = kindOf(p.schema);
    if (kind === 'array') {
      return text.split(',').map(function (v) { return v.trim(); });
    }
    if (kind === 'object') {
      var value;
      try {
        value = JSON.parse(text);
      } catch (e) {
        value = null;
      }
      if (!value || typeof value !== 'object' || Array.isArray(value)) {
        throw new Error(p.name + ' must be a JSON object, like {"key": "value"}.');
      }
      return value;
    }
    return text;
  }

  // One value as text. A nested object or array inside an object has no standard serialization
  // outside deepObject, so it goes as JSON, which is what a server accepting one at all expects.
  function scalar(value) {
    return value === null || value === undefined ? ''
      : typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  function entriesOf(value) {
    return Object.keys(value).map(function (k) { return [k, scalar(value[k])]; });
  }

  // simple style, shared by path and header: a,b for an array; k,v,k,v for an object, or k=v,k=v
  // exploded. `encode` is applied to every name and value, never to the separators.
  function serializeSimple(p, value, explode, encode) {
    if (Array.isArray(value)) {
      return value.map(encode).join(',');
    }
    if (value && typeof value === 'object') {
      return entriesOf(value).map(function (e) {
        return encode(e[0]) + (explode ? '=' : ',') + encode(e[1]);
      }).join(',');
    }
    return encode(value);
  }

  // label (.a.b), matrix (;id=a;id=b) or simple, percent-encoded into the path.
  function serializePath(p, value) {
    var s = styleOf(p);
    var enc = encodeURIComponent;
    if (s.style === 'label') {
      var sep = s.explode ? '.' : ',';
      if (Array.isArray(value)) {
        return '.' + value.map(enc).join(sep);
      }
      if (value && typeof value === 'object') {
        return '.' + entriesOf(value).map(function (e) {
          return enc(e[0]) + (s.explode ? '=' : ',') + enc(e[1]);
        }).join(sep);
      }
      return '.' + enc(value);
    }
    if (s.style === 'matrix') {
      var name = enc(p.name);
      if (Array.isArray(value)) {
        return s.explode
          ? value.map(function (v) { return ';' + name + '=' + enc(v); }).join('')
          : ';' + name + '=' + value.map(enc).join(',');
      }
      if (value && typeof value === 'object') {
        return s.explode
          ? entriesOf(value).map(function (e) { return ';' + enc(e[0]) + '=' + enc(e[1]); }).join('')
          : ';' + name + '=' + entriesOf(value).map(function (e) {
            return enc(e[0]) + ',' + enc(e[1]);
          }).join(',');
      }
      return ';' + name + '=' + enc(value);
    }
    return serializeSimple(p, value, s.explode, enc);
  }

  // deepObject: filter[color]=red, recursing into nested objects as filter[size][min]=1 and
  // repeating the name for an array's items — the convention servers that take one follow.
  function deepPairs(prefix, value, pairs) {
    if (Array.isArray(value)) {
      value.forEach(function (v) { deepPairs(prefix, v, pairs); });
    } else if (value && typeof value === 'object') {
      Object.keys(value).forEach(function (k) { deepPairs(prefix + '[' + k + ']', value[k], pairs); });
    } else {
      pairs.push([prefix, scalar(value)]);
    }
    return pairs;
  }

  // [name, value] pairs, unencoded: URLSearchParams encodes them when the URL is built.
  function serializeQuery(p, value) {
    var s = styleOf(p);
    var name = p.name;
    if (s.style === 'deepObject' && value && typeof value === 'object') {
      return deepPairs(name, value, []);
    }
    var delimiter = s.style === 'spaceDelimited' ? ' ' : s.style === 'pipeDelimited' ? '|' : ',';
    if (Array.isArray(value)) {
      return s.explode
        ? value.map(function (v) { return [name, v]; })
        : [[name, value.join(delimiter)]];
    }
    if (value && typeof value === 'object') {
      var entries = entriesOf(value);
      return s.explode
        ? entries
        : [[name, entries.map(function (e) { return e[0] + delimiter + e[1]; }).join(delimiter)]];
    }
    return [[name, value]];
  }

  function buildRequest(state) {
    var entry = state.entry;
    var req = { headers: new Headers(), query: [], body: undefined };
    var path = entry.path;
    var missing = [];

    state.params.forEach(function (item) {
      var p = item.param;
      var value = item.input.value;
      item.input.removeAttribute('aria-invalid');
      if (value === '') {
        if (item.input.required) {
          missing.push(p.name);
          item.input.setAttribute('aria-invalid', 'true');
        }
        return;
      }
      var parsed = paramValue(p, value);
      if (p.in === 'path') {
        path = path.split('{' + p.name + '}').join(serializePath(p, parsed));
      } else if (p.in === 'query') {
        serializeQuery(p, parsed).forEach(function (pair) { req.query.push(pair); });
      } else if (p.in === 'header') {
        req.headers.set(p.name, serializeSimple(p, parsed, styleOf(p).explode, String));
      }
    });

    if (missing.length) {
      throw new Error('Required: ' + missing.join(', ') + '.');
    }

    state.auth.forEach(function (s) {
      s.handler.apply(req, credentials[s.name] || {}, s.scheme);
    });

    if (state.accept && state.accept.value) {
      req.headers.set('Accept', state.accept.value);
    }

    if (state.body) {
      var type = state.body.typeSelect.value;
      var text = state.body.editor.value;
      var files = state.body.files();
      var noFile = files.filter(function (f) {
        f.input.removeAttribute('aria-invalid');
        return f.required && !f.input.files.length;
      });
      if (noFile.length) {
        noFile.forEach(function (f) { f.input.setAttribute('aria-invalid', 'true'); });
        throw new Error('Choose a file for: ' + noFile.map(function (f) { return f.name; }).join(', ') + '.');
      }
      var encoded;
      try {
        encoded = bodyHandlerFor(type, state.body.content[type]).encode(text, files);
      } catch (e) {
        throw new Error('The body is not valid for ' + type + ': ' + e.message);
      }
      if (encoded === undefined && state.body.required) {
        throw new Error(bodyHandlerFor(type, state.body.content[type]).whole
          ? 'Choose a file to send as the body.'
          : 'The body is required.');
      }
      if (encoded !== undefined) {
        req.body = encoded;
        // FormData's Content-Type carries a boundary only the browser knows, so it writes it. A
        // wildcard like image/* is no Content-Type at all; a file says what it is instead.
        if (encoded instanceof Blob && type.indexOf('*') >= 0) {
          req.headers.set('Content-Type', encoded.type || 'application/octet-stream');
        } else if (!(encoded instanceof FormData)) {
          req.headers.set('Content-Type', type);
        }
      }
    }

    state.extraHeaders().forEach(function (pair) {
      try {
        req.headers.set(pair[0], pair[1]);
      } catch (e) {
        throw new Error('"' + pair[0] + '" can\'t be sent as a header: a name is letters, digits '
          + 'and !#$%&\'*+-.^_`|~, and a value has no line breaks.');
      }
    });

    var url = new URL(state.server.url().replace(/\/+$/, '') + path);
    req.query.forEach(function (pair) { url.searchParams.append(pair[0], pair[1]); });
    req.url = url;
    return req;
  }

  function send() {
    var state = current;
    if (!state || !dialog || !dialog.open) {
      return;
    }

    var req;
    try {
      req = buildRequest(state);
    } catch (e) {
      showError(e.message);
      return;
    }

    if (inflight) {
      inflight.abort();
    }
    var controller = new AbortController();
    inflight = controller;
    state.cancelled = false;
    state.responseDone = false;
    state.sendButton.disabled = true;
    state.cancelButton.hidden = false;
    state.response.replaceChildren(h('p', { class: 'hlibz-tryit-hint', text: 'Sending…' }));

    var started = performance.now();
    var arrived = false;
    fetch(req.url.href, {
      method: state.entry.method.toUpperCase(),
      headers: req.headers,
      body: req.body,
      credentials: config.cookies ? 'same-origin' : 'omit',
      signal: controller.signal,
    }).then(function (response) {
      arrived = true;
      // Bytes rather than text, so a binary body can be measured and offered as a file instead of
      // being decoded into noise.
      return response.arrayBuffer().then(function (bytes) {
        if (current === state) {
          renderResponse(state, response, bytes, performance.now() - started, req.url);
        }
      });
    }).catch(function (error) {
      if (current !== state) {
        return;
      }
      if (error && error.name === 'AbortError') {
        // Aborted by Cancel; otherwise by a newer Send or by closing, which show their own thing.
        if (state.cancelled) {
          state.response.replaceChildren(h('p', { class: 'hlibz-tryit-hint', text: 'Cancelled.' }));
          state.responseDone = true;
        }
        return;
      }
      showError(arrived
        ? 'The response arrived but could not be read: ' + error.message
        : req.url.origin === location.origin
          ? 'The request failed before a response arrived: ' + error.message
          : 'The request to ' + req.url.origin + ' was blocked. Another origin has to allow it '
            + 'with CORS, and this page\'s Content-Security-Policy has to list it in connect-src.');
    }).finally(function () {
      if (inflight === controller) {
        inflight = null;
      }
      // A newer Send has the buttons now; anything else (done, cancelled, closed) gives them back.
      if (!inflight) {
        state.sendButton.disabled = false;
        state.cancelButton.hidden = true;
      }
    });
  }

  // The request as a curl command line, for a terminal. POSIX single quotes, inside which nothing
  // is special but the quote itself.
  function shellQuote(text) {
    return '\'' + String(text).replace(/'/g, '\'\\\'\'') + '\'';
  }

  function curlOf(method, req) {
    var parts = ['curl'
      + (method === 'head' ? ' --head' : method === 'get' ? '' : ' -X ' + method.toUpperCase())
      + ' ' + shellQuote(req.url.href)];
    req.headers.forEach(function (value, name) {
      parts.push('-H ' + shellQuote(name + ': ' + value));
    });
    if (req.body instanceof FormData) {
      // --form-string, not -F, for text: -F reads a value starting with @ or < as a file. A file
      // goes by its name alone, since the page never learns where it lives on disk.
      req.body.forEach(function (value, name) {
        parts.push(typeof value === 'string'
          ? '--form-string ' + shellQuote(name + '=' + value)
          : '-F ' + shellQuote(name + '=@' + value.name + (value.type ? ';type=' + value.type : '')));
      });
    } else if (req.body instanceof Blob) {
      parts.push('--data-binary ' + shellQuote('@' + (req.body.name || 'file')));
    } else if (req.body !== undefined) {
      parts.push('--data-raw ' + shellQuote(req.body));
    }
    return parts.join(' \\\n  ');
  }

  // The request as a fetch() call, for a browser console or a script. Strings go through
  // JSON.stringify, which writes a valid JavaScript literal for any text. A file can't be put in
  // code, so it is left as a named placeholder to fill in.
  function fetchOf(method, req) {
    var lines = [];
    var init = ['  method: ' + JSON.stringify(method.toUpperCase()) + ','];
    var headers = [];
    req.headers.forEach(function (value, name) {
      headers.push('    ' + JSON.stringify(name) + ': ' + JSON.stringify(value) + ',');
    });
    if (headers.length) {
      init.push('  headers: {', headers.join('\n'), '  },');
    }
    if (req.body instanceof FormData) {
      lines.push('const body = new FormData();');
      req.body.forEach(function (value, name) {
        lines.push(typeof value === 'string'
          ? 'body.append(' + JSON.stringify(name) + ', ' + JSON.stringify(value) + ');'
          : 'body.append(' + JSON.stringify(name) + ', file, ' + JSON.stringify(value.name)
            + '); // a File or Blob: ' + value.name);
      });
      init.push('  body,');
    } else if (req.body instanceof Blob) {
      lines.push('const body = file; // a File or Blob: ' + (req.body.name || 'the body'));
      init.push('  body,');
    } else if (req.body !== undefined) {
      init.push('  body: ' + JSON.stringify(req.body) + ',');
    }
    if (!config.cookies) {
      init.push('  credentials: "omit",');
    }
    lines.push('const response = await fetch(' + JSON.stringify(req.url.href) + ', {',
      init.join('\n'), '});');
    return lines.join('\n');
  }

  // A button's label briefly replaced by what just happened to it.
  function flash(button, message) {
    if (!button.hlibzLabel) {
      button.hlibzLabel = button.textContent;
    }
    button.textContent = message;
    clearTimeout(button.hlibzTimer);
    button.hlibzTimer = setTimeout(function () { button.textContent = button.hlibzLabel; }, 1500);
  }

  // Raw or pretty, for a JSON response body. One choice for the whole page, so it sticks while you
  // send request after request.
  var showPretty = true;

  // Past this size a tree of nodes costs more than it helps: Pretty falls back to indented text.
  var TREE_LIMIT = 512 * 1024;

  // A number exactly as the server wrote it. JSON.parse would turn 1234567890123456789 into
  // 1234567890123456800, and the Pretty view would then show, and copy, an id that doesn't exist.
  function RawNumber(text) {
    this.text = text;
  }

  // JSON.parse's grammar, but numbers kept as RawNumber and objects without a prototype, so a
  // "__proto__" key is just a key. Throws a SyntaxError where JSON.parse would.
  var NUMBER = /-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/y;
  var STRING = /"(?:[^"\\\u0000-\u001f]|\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4}))*"/y;

  function parseJson(text) {
    var at = 0;

    function fail() {
      throw new SyntaxError('Unexpected token at position ' + at);
    }
    function space() {
      while (at < text.length && ' \t\n\r'.indexOf(text.charAt(at)) >= 0) {
        at++;
      }
    }
    function match(pattern) {
      pattern.lastIndex = at;
      var found = pattern.exec(text);
      if (!found) {
        fail();
      }
      at = pattern.lastIndex;
      return found[0];
    }
    function expect(char) {
      space();
      if (text.charAt(at) !== char) {
        fail();
      }
      at++;
    }
    function value() {
      space();
      var c = text.charAt(at);
      if (c === '"') {
        return JSON.parse(match(STRING));
      }
      if (c === '{') {
        at++;
        var object = Object.create(null);
        space();
        if (text.charAt(at) === '}') {
          at++;
          return object;
        }
        do {
          space();
          var key = JSON.parse(match(STRING));
          expect(':');
          object[key] = value();
          space();
        } while (text.charAt(at) === ',' && ++at);
        expect('}');
        return object;
      }
      if (c === '[') {
        at++;
        var array = [];
        space();
        if (text.charAt(at) === ']') {
          at++;
          return array;
        }
        do {
          array.push(value());
          space();
        } while (text.charAt(at) === ',' && ++at);
        expect(']');
        return array;
      }
      var words = { t: 'true', f: 'false', n: 'null' };
      if (words[c]) {
        if (text.slice(at, at + words[c].length) !== words[c]) {
          fail();
        }
        at += words[c].length;
        return JSON.parse(words[c]);
      }
      return new RawNumber(match(NUMBER));
    }

    var result = value();
    space();
    if (at < text.length) {
      fail();
    }
    return result;
  }

  // JSON.stringify(value, null, 2) for what parseJson returns, numbers written as they came.
  function prettyJson(value, indent) {
    indent = indent || '';
    if (value instanceof RawNumber) {
      return value.text;
    }
    if (value === null || typeof value !== 'object') {
      return JSON.stringify(value);
    }
    var inner = indent + '  ';
    if (Array.isArray(value)) {
      return value.length
        ? '[\n' + value.map(function (v) { return inner + prettyJson(v, inner); }).join(',\n')
          + '\n' + indent + ']'
        : '[]';
    }
    var names = Object.keys(value);
    return names.length
      ? '{\n' + names.map(function (k) {
        return inner + JSON.stringify(k) + ': ' + prettyJson(value[k], inner);
      }).join(',\n') + '\n' + indent + '}'
      : '{}';
  }

  // Pretty JSON as a tree whose objects and arrays fold. Built from nodes and textContent only, like
  // everything else here: a response is data from a server, never markup.
  function jsonTree(value) {
    return h('div', { class: 'hlibz-tryit-code hlibz-tryit-output hlibz-tryit-json', tabindex: '0' },
      jsonNode(value, null, true));
  }

  function jsonNode(value, key, last) {
    var label = key === null ? null : [
      h('span', { class: 'hlibz-tryit-json-key', text: JSON.stringify(key) }),
      ': ',
    ];
    var comma = last ? '' : ',';

    if (value instanceof RawNumber) {
      return h('div', { class: 'hlibz-tryit-json-line' },
        label,
        h('span', { class: 'hlibz-tryit-json-number', text: value.text }),
        comma);
    }
    if (value === null || typeof value !== 'object') {
      var kind = value === null ? 'null' : typeof value;
      return h('div', { class: 'hlibz-tryit-json-line' },
        label,
        h('span', { class: 'hlibz-tryit-json-' + kind, text: JSON.stringify(value) }),
        comma);
    }

    var isArray = Array.isArray(value);
    var names = isArray ? null : Object.keys(value);
    var count = isArray ? value.length : names.length;
    var open = isArray ? '[' : '{';
    var close = isArray ? ']' : '}';
    if (count === 0) {
      return h('div', { class: 'hlibz-tryit-json-line' }, label, open + close + comma);
    }

    var children = h('div', { class: 'hlibz-tryit-json-children' });
    for (var i = 0; i < count; i++) {
      children.appendChild(isArray
        ? jsonNode(value[i], null, i === count - 1)
        : jsonNode(value[names[i]], names[i], i === count - 1));
    }
    var folded = h('span', { class: 'hlibz-tryit-json-folded', hidden: true },
      h('span', { class: 'hlibz-tryit-json-ellipsis', text: '\u2026' }),
      close + comma,
      h('span', {
        class: 'hlibz-tryit-json-count',
        text: count + (isArray ? (count === 1 ? ' item' : ' items') : (count === 1 ? ' key' : ' keys')),
      }));
    var closing = h('div', { class: 'hlibz-tryit-json-line' }, close + comma);
    var toggle = h('button', {
      type: 'button',
      class: 'hlibz-tryit-json-toggle',
      'aria-expanded': 'true',
      'aria-label': 'Collapse',
      onclick: function (event) {
        var expand = toggle.getAttribute('aria-expanded') !== 'true';
        // Alt-click folds or unfolds everything inside too, as browsers' own JSON viewers do.
        var scope = event.altKey ? node.querySelectorAll('.hlibz-tryit-json-toggle') : [toggle];
        Array.prototype.forEach.call(scope, function (t) { t.hlibzSet(expand); });
        if (event.altKey) { setExpanded(expand); }
      },
    });
    function setExpanded(expand) {
      toggle.setAttribute('aria-expanded', String(expand));
      toggle.setAttribute('aria-label', expand ? 'Collapse' : 'Expand');
      children.hidden = !expand;
      closing.hidden = !expand;
      folded.hidden = expand;
    }
    toggle.hlibzSet = setExpanded;

    var node = h('div', { class: 'hlibz-tryit-json-node' },
      h('div', { class: 'hlibz-tryit-json-line' }, toggle, label, open, folded),
      children,
      closing);
    return node;
  }

  function copyText(text) {
    if (navigator.clipboard && window.isSecureContext) {
      return navigator.clipboard.writeText(text);
    }
    // Plain http on a host other than localhost: no async clipboard, so the old way.
    var area = h('textarea', { class: 'hlibz-tryit-offscreen', readonly: true });
    area.value = text;
    (document.querySelector('dialog.hlibz-tryit[open]') || document.body).appendChild(area);
    area.select();
    var ok = false;
    try { ok = document.execCommand('copy'); } catch (e) { /* reported below */ }
    area.remove();
    return ok ? Promise.resolve() : Promise.reject(new Error('copy failed'));
  }

  // Any body gets Copy; only a JSON one gets Pretty and Raw.
  function responseBody(text, maybeJson) {
    var value;
    var isJson = false;
    if (maybeJson) {
      try {
        value = parseJson(text);
        isJson = true;
      } catch (e) { /* not JSON after all: shown as it came */ }
    }
    var pretty = isJson ? prettyJson(value) : null;

    var view = h('div', { class: 'hlibz-tryit-view' });
    var tree = null;
    function show() {
      var usePretty = isJson && showPretty;
      if (usePretty && text.length <= TREE_LIMIT) {
        tree = tree || jsonTree(value);
        view.replaceChildren(tree);
      } else {
        view.replaceChildren(h('pre', {
          class: 'hlibz-tryit-code hlibz-tryit-output',
          tabindex: '0',
          text: usePretty ? pretty : text,
        }));
      }
    }

    var copy = h('button', {
      type: 'button',
      class: 'hlibz-tryit-ghost hlibz-tryit-push',
      text: 'Copy',
      onclick: function () {
        copyText(isJson && showPretty ? pretty : text).then(
          function () { flash(copy, 'Copied'); },
          function () { flash(copy, 'Copy failed'); });
      },
    });

    var modes = null;
    if (isJson) {
      var option = function (label, isPretty) {
        return h('button', {
          type: 'button',
          class: 'hlibz-tryit-mode',
          'aria-pressed': String(showPretty === isPretty),
          text: label,
          onclick: function () {
            showPretty = isPretty;
            modes.querySelectorAll('button').forEach(function (b) {
              b.setAttribute('aria-pressed', String(b.textContent === label));
            });
            show();
          },
        });
      };
      modes = h('div', { class: 'hlibz-tryit-modes', role: 'group', 'aria-label': 'Body view' },
        option('Pretty', true),
        option('Raw', false));
    }

    show();
    return h('div', null, h('div', { class: 'hlibz-tryit-body-bar' }, modes, copy), view);
  }

  function formatSize(bytes) {
    return bytes < 1024 ? bytes + ' B'
      : bytes < 1024 * 1024 ? (bytes / 1024).toFixed(1) + ' KB'
      : (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // Whether a body of this Content-Type reads as text. No type at all is given the benefit of the
  // doubt: a server that forgot the header far more often sent text than an image.
  function isTextual(type) {
    var essence = type.split(';')[0].trim().toLowerCase();
    return essence === ''
      || /^text\//.test(essence)
      || /[/+](json|xml|yaml|x-yaml)$/.test(essence)
      || /^application\/(javascript|ecmascript|x-www-form-urlencoded|graphql|x-ndjson|jsonl?)$/
        .test(essence);
  }

  function decode(bytes, type) {
    var charset = /;\s*charset\s*=\s*"?([^";]+)/i.exec(type);
    try {
      return new TextDecoder(charset ? charset[1].trim() : 'utf-8').decode(bytes);
    } catch (e) {
      return new TextDecoder('utf-8').decode(bytes); // a charset label TextDecoder doesn't know
    }
  }

  // A binary body as a file to save, under the name the server suggested if it gave one.
  function binaryBody(bytes, type, disposition) {
    var named = /filename\*?\s*=\s*(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition || '');
    var name = 'response';
    if (named) {
      try { name = decodeURIComponent(named[1]); } catch (e) { name = named[1]; }
    }
    var download = h('button', {
      type: 'button',
      class: 'hlibz-tryit-ghost hlibz-tryit-push',
      text: 'Download',
      onclick: function () {
        var href = URL.createObjectURL(new Blob([bytes], { type: type }));
        var link = h('a', { href: href, download: name, class: 'hlibz-tryit-offscreen' });
        // Inside the dialog, as copyText's textarea is: the modal makes the rest of the page inert.
        (dialog || document.body).appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(href); }, 10000);
      },
    });
    return h('div', { class: 'hlibz-tryit-body-bar' },
      h('span', { class: 'hlibz-tryit-hint', text: 'Binary body, ' + (type || 'no Content-Type') + '.' }),
      download);
  }

  // What the operation says this status means: the exact code, then its range (4XX), then
  // default. Null when it says nothing, which is itself worth showing.
  function documentedResponse(op, status) {
    var responses = op.responses || {};
    var code = String(status);
    var range = code.charAt(0) + 'XX';
    var keys = Object.keys(responses);
    var match = keys.filter(function (k) { return k === code; })[0]
      || keys.filter(function (k) { return k.toUpperCase() === range; })[0]
      || keys.filter(function (k) { return k === 'default'; })[0];
    if (!match) {
      return null;
    }
    var response = deref(responses[match]) || {};
    return { key: match, description: String(response.description || '').split('\n')[0].trim() };
  }

  function renderResponse(state, response, bytes, elapsed, url) {
    state.responseDone = true;
    var type = response.headers.get('content-type') || '';
    var json = /[/+]json\b/i.test(type);

    var headers = [];
    response.headers.forEach(function (value, name) {
      headers.push(name + ': ' + value);
    });

    var body;
    if (!bytes.byteLength) {
      body = h('p', { class: 'hlibz-tryit-hint', text: 'No body.' });
    } else if (isTextual(type)) {
      body = responseBody(decode(bytes, type), json);
    } else {
      body = binaryBody(bytes, type, response.headers.get('content-disposition'));
    }

    // fetch follows redirects without a word, so a 302 to a sign-in page arrives as that page's
    // "200 OK". The status is then some other URL's, and matching it against this operation's
    // documented responses would vouch for it; the redirect is said instead.
    var redirect = response.redirected
      ? h('p', { class: 'hlibz-tryit-undocumented',
        text: 'Redirected to ' + response.url + '. The status is that URL\'s, not this operation\'s.' })
      : null;

    // Spec text goes in as text, never as the Markdown ReDoc would render it.
    var documented = redirect ? null : documentedResponse(state.entry.op, response.status);
    // "Unauthorized" under a 401 Unauthorized says nothing twice.
    var says = documented && documented.description
      && documented.description.toLowerCase() !== (response.statusText || '').toLowerCase();
    var meaning = redirect ? redirect
      : documented
      ? (says
        ? h('p', { class: 'hlibz-tryit-hint hlibz-tryit-meaning',
          text: (documented.key === String(response.status) ? '' : documented.key + ': ')
            + documented.description })
        : null)
      : h('p', { class: 'hlibz-tryit-undocumented',
        text: 'Not a status this operation documents.' });

    var tone = response.status >= 500 ? 'error' : response.status >= 400 ? 'warning' : 'success';
    // replaceChildren, unlike h(), would write a null child out as the text "null".
    state.response.replaceChildren.apply(state.response, [
      h('div', { class: 'hlibz-tryit-status' },
        h('span', { class: 'hlibz-tryit-badge hlibz-tryit-' + tone, text: String(response.status) }),
        h('span', { text: response.statusText || '' }),
        h('span', { class: 'hlibz-tryit-hint', text: Math.round(elapsed) + ' ms' }),
        h('span', { class: 'hlibz-tryit-hint', text: formatSize(bytes.byteLength) })),
      meaning,
      h('p', { class: 'hlibz-tryit-hint hlibz-tryit-url', text: url.href }),
      h('details', { class: 'hlibz-tryit-headers' },
        h('summary', { text: 'Headers (' + headers.length + ')' }),
        h('pre', { class: 'hlibz-tryit-code', text: headers.join('\n') })),
      body,
    ].filter(Boolean));
  }

  // ---------------------------------------------------------------------------------------------
  // Mounting the buttons

  function mount() {
    var sections = document.querySelectorAll('[data-item-kind="httpItem"][data-section-id]');
    for (var i = 0; i < sections.length; i++) {
      var section = sections[i];
      var actions = section.querySelector('[data-testid="page-actions"]');
      if (!actions || actions.querySelector('.hlibz-tryit-open')) {
        continue;
      }
      var target = targetOf(section.getAttribute('data-section-id'));
      var entry = target && operationFor(target.path, target.method);
      if (!entry || entry.op['x-hideTryItPanel'] === true
          || (config.safeOnly && SAFE_METHODS.indexOf(entry.method) < 0)) {
        continue;
      }
      actions.insertBefore(makeOpener(entry), actions.firstChild);
    }
  }

  function makeOpener(entry) {
    var button = h('button', { type: 'button', class: 'hlibz-tryit-open', text: 'Try it' });
    button.addEventListener('click', function () {
      // Re-read on click: the spec can't change, but ReDoc may have rebuilt the section.
      open(operationFor(entry.path, entry.method) || entry, button);
    });
    return button;
  }

  // Batched on a timer rather than requestAnimationFrame: a background tab runs no frames, and a
  // section React re-rendered there would sit without its button until something else changed.
  var scheduled = false;
  function scheduleMount() {
    if (scheduled) {
      return;
    }
    scheduled = true;
    setTimeout(function () {
      scheduled = false;
      mount();
    }, 50);
  }

  fetch(new URL(config.specUrl, location.href).href, {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  }).then(function (response) {
    if (!response.ok) {
      throw new Error('HTTP ' + response.status);
    }
    return response.text();
  }).then(function (text) {
    try {
      spec = JSON.parse(text);
    } catch (e) {
      // ReDoc reads YAML too; this console doesn't carry a YAML parser.
      throw new Error('it is not JSON, and the console reads JSON documents only');
    }
    mount();
    // ReDoc renders operations as they come near the viewport, so new ones keep arriving. The
    // console's own changes — a response tree can be thousands of nodes — never add a section.
    new MutationObserver(function (records) {
      for (var i = 0; i < records.length; i++) {
        if (!dialog || !dialog.contains(records[i].target)) {
          scheduleMount();
          return;
        }
      }
    }).observe(document.body, { childList: true, subtree: true });
  }).catch(function (error) {
    // The page itself still works; only the console is missing.
    console.warn('Try it: no buttons, because the OpenAPI document could not be read: '
      + error.message + '.');
  });
})();
