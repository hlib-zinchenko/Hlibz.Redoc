// Try-it console for ReDoc 3, served by Hlibz.Redoc.AspNetCore when EnableTryIt() is on.
//
// It never touches ReDoc's React state. It reads the spec itself, mounts a "Try it" button into
// each operation's page-actions, and opens a <dialog> of its own. The DOM contract it relies on
// is ReDoc 3's: [data-item-kind="httpItem"][data-section-id] per operation, whose id ends in
// /paths/{JSON-pointer path}/{method}, and [data-testid="page-actions"] inside it. Measured on
// 3.0.1; TRY-IT.md has the details.
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
    persist: self.getAttribute('data-persist-credentials') === 'true',
    cookies: self.getAttribute('data-send-cookies') !== 'false',
  };

  var METHODS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace', 'query'];
  var STORAGE_KEY = 'hlibz.tryit:' + new URL(config.specUrl, location.href).pathname;

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

  // The example a media type entry declares, before falling back to its schema.
  function mediaExample(media) {
    if (!media) {
      return undefined;
    }
    if (media.example !== undefined) {
      return media.example;
    }
    if (media.examples) {
      var first = Object.keys(media.examples)[0];
      var ex = first && deref(media.examples[first]);
      if (ex && ex.value !== undefined) {
        return ex.value;
      }
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
  // memory unless the site opted in to sessionStorage; never localStorage.
  var credentials = {};
  if (config.persist) {
    try {
      credentials = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || '{}') || {};
    } catch (e) { credentials = {}; }
  }

  function saveCredentials() {
    if (!config.persist) {
      return;
    }
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(credentials));
    } catch (e) { /* storage unavailable: memory only */ }
  }

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
    initial: function (media) {
      var example = mediaExample(media);
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

  // Anything else is sent as typed, with its declared Content-Type. Not a promise of support,
  // just not a refusal either.
  var rawBody = {
    raw: true,
    matches: function () { return true; },
    initial: function () { return ''; },
    encode: function (text) { return text === '' ? undefined : text; },
  };

  function bodyHandlerFor(type) {
    for (var i = 0; i < bodyHandlers.length; i++) {
      if (bodyHandlers[i].matches(type)) {
        return bodyHandlers[i];
      }
    }
    return rawBody;
  }

  // ---------------------------------------------------------------------------------------------
  // Servers

  function serversFor(entry) {
    var declared = entry.op.servers || (spec.paths[entry.path] || {}).servers || spec.servers;
    var list = [];
    (declared || []).forEach(function (server) {
      if (!server || typeof server.url !== 'string') {
        return;
      }
      var url = server.url.replace(/\{([^}]+)\}/g, function (match, name) {
        var variable = server.variables && server.variables[name];
        return variable && variable.default !== undefined ? variable.default : match;
      });
      try {
        list.push({ url: new URL(url, config.appBase).href, label: server.description || url });
      } catch (e) { /* not a URL we can send to */ }
    });

    // No servers means "/" in OpenAPI, which on a PathBase or behind a proxy is not where the API
    // is. The page knows where the app base is, so that is the default instead.
    if (!list.length) {
      list.push({ url: config.appBase, label: 'This app' });
    }

    var sameOrigin = list.filter(function (s) { return new URL(s.url).origin === location.origin; });
    return { list: list, selected: (sameOrigin[0] || list[0]).url };
  }

  // ---------------------------------------------------------------------------------------------
  // The dialog

  var dialog = null;
  var current = null;
  var inflight = null;

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
    var value = p.example !== undefined ? p.example : sampleOf(p.schema);
    if (value === undefined || value === null) {
      return '';
    }
    if (Array.isArray(value)) {
      return value.join(',');
    }
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  function open(entry, opener) {
    ensureDialog();
    if (inflight) {
      inflight.abort();
      inflight = null;
    }

    var op = entry.op;
    var servers = serversFor(entry);
    var schemes = schemesFor(op);
    var state = { entry: entry, opener: opener, params: [], auth: [], body: null, server: null };
    current = state;

    // Server
    var serverSelect = h('select', { class: 'hlibz-tryit-input' },
      servers.list.map(function (s) {
        return h('option', { value: s.url, selected: s.url === servers.selected, text: s.label === s.url ? s.url : s.label + ' — ' + s.url });
      }));
    state.server = serverSelect;

    // Auth
    var authSection = null;
    if (schemes.length) {
      authSection = h('section', { class: 'hlibz-tryit-section' },
        h('h3', { text: 'Authorization' }),
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
              oninput: function (e) {
                values[f.name] = e.target.value;
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
          var input = h('input', {
            class: 'hlibz-tryit-input',
            type: 'text',
            spellcheck: 'false',
            value: paramDefault(p),
            required: !!p.required || where === 'path',
          });
          state.params.push({ param: p, input: input });
          var label = h('span', null, h('code', { text: p.name }),
            (p.required || where === 'path') ? h('span', { class: 'hlibz-tryit-required', text: ' required' }) : null);
          var schema = deref(p.schema) || {};
          var hint = [schema.type, schema.format].filter(Boolean).join(', ')
            + (schema.type === 'array' ? ' — comma-separated' : '');
          return field(label, input, hint);
        }));
    });

    // Body
    var bodySection = null;
    var requestBody = deref(op.requestBody);
    if (requestBody && requestBody.content) {
      var types = Object.keys(requestBody.content);
      var typeSelect = h('select', { class: 'hlibz-tryit-input' },
        types.map(function (t) { return h('option', { value: t, text: t }); }));
      // Prefer a type we actually understand.
      var preferred = types.filter(function (t) { return !bodyHandlerFor(t).raw; })[0];
      if (preferred) {
        typeSelect.value = preferred;
      }
      var editor = h('textarea', { class: 'hlibz-tryit-input hlibz-tryit-code', rows: 12, spellcheck: 'false' });
      var note = h('span', { class: 'hlibz-tryit-hint' });
      var fill = function () {
        var handler = bodyHandlerFor(typeSelect.value);
        editor.value = handler.initial(requestBody.content[typeSelect.value]);
        note.textContent = handler.raw ? 'Sent as typed: this media type has no editor here yet.' : '';
      };
      typeSelect.addEventListener('change', fill);
      fill();
      state.body = { typeSelect: typeSelect, editor: editor, required: !!requestBody.required };
      bodySection = h('section', { class: 'hlibz-tryit-section' },
        h('h3', null, 'Body', requestBody.required ? h('span', { class: 'hlibz-tryit-required', text: ' required' }) : null),
        types.length > 1 ? field('Content type', typeSelect) : h('p', { class: 'hlibz-tryit-hint', text: types[0] }),
        editor,
        note);
    }

    var method = entry.method.toUpperCase();
    var sendButton = h('button', {
      type: 'button',
      class: 'hlibz-tryit-send' + (entry.method === 'get' || entry.method === 'head' ? '' : ' hlibz-tryit-send-unsafe'),
      onclick: send,
      text: 'Send ' + method + ' request',
    });
    state.sendButton = sendButton;
    state.response = h('div', { class: 'hlibz-tryit-response', 'aria-live': 'polite' });

    dialog.replaceChildren(
      h('header', { class: 'hlibz-tryit-header' },
        h('span', { class: 'hlibz-tryit-method hlibz-tryit-method-' + entry.method, text: method }),
        h('h2', { id: 'hlibz-tryit-title', text: entry.path }),
        h('button', { type: 'button', class: 'hlibz-tryit-close', 'aria-label': 'Close', text: '×', onclick: function () { dialog.close(); } })),
      h('div', { class: 'hlibz-tryit-body' },
        op.summary ? h('p', { class: 'hlibz-tryit-summary', text: op.summary }) : null,
        h('section', { class: 'hlibz-tryit-section' }, field('Server', serverSelect)),
        authSection,
        paramSections,
        bodySection,
        h('div', { class: 'hlibz-tryit-actions' },
          sendButton,
          h('span', { class: 'hlibz-tryit-hint', text: (navigator.platform.indexOf('Mac') >= 0 ? '⌘' : 'Ctrl') + '+Enter' })),
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
      var schema = deref(p.schema) || {};
      var values = schema.type === 'array' ? value.split(',').map(function (v) { return v.trim(); }) : [value];
      if (p.in === 'path') {
        path = path.split('{' + p.name + '}').join(values.map(encodeURIComponent).join(','));
      } else if (p.in === 'query') {
        if (p.explode === false) {
          req.query.push([p.name, values.join(',')]);
        } else {
          values.forEach(function (v) { req.query.push([p.name, v]); });
        }
      } else if (p.in === 'header') {
        req.headers.set(p.name, values.join(','));
      }
    });

    if (missing.length) {
      throw new Error('Required: ' + missing.join(', ') + '.');
    }

    state.auth.forEach(function (s) {
      s.handler.apply(req, credentials[s.name] || {}, s.scheme);
    });

    if (state.body) {
      var type = state.body.typeSelect.value;
      var text = state.body.editor.value;
      var encoded;
      try {
        encoded = bodyHandlerFor(type).encode(text);
      } catch (e) {
        throw new Error('The body is not valid for ' + type + ': ' + e.message);
      }
      if (encoded === undefined && state.body.required) {
        throw new Error('The body is required.');
      }
      if (encoded !== undefined) {
        req.body = encoded;
        req.headers.set('Content-Type', type);
      }
    }

    var url = new URL(state.server.value.replace(/\/+$/, '') + path);
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
    state.sendButton.disabled = true;
    state.response.replaceChildren(h('p', { class: 'hlibz-tryit-hint', text: 'Sending…' }));

    var started = performance.now();
    fetch(req.url.href, {
      method: state.entry.method.toUpperCase(),
      headers: req.headers,
      body: req.body,
      credentials: config.cookies ? 'same-origin' : 'omit',
      signal: controller.signal,
    }).then(function (response) {
      return response.text().then(function (text) {
        if (current === state) {
          renderResponse(state, response, text, performance.now() - started, req.url);
        }
      });
    }, function (error) {
      if (error && error.name === 'AbortError') {
        return;
      }
      if (current === state) {
        showError(req.url.origin === location.origin
          ? 'The request failed before a response arrived: ' + error.message
          : 'The request to ' + req.url.origin + ' was blocked. Another origin has to allow it '
            + 'with CORS, and this page\'s Content-Security-Policy has to list it in connect-src.');
      }
    }).finally(function () {
      if (inflight === controller) {
        inflight = null;
      }
      state.sendButton.disabled = false;
    });
  }

  // Raw or pretty, for a JSON response body. One choice for the whole page, so it sticks while you
  // send request after request.
  var showPretty = true;

  // Past this size a tree of nodes costs more than it helps: Pretty falls back to indented text.
  var TREE_LIMIT = 512 * 1024;

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
        value = JSON.parse(text);
        isJson = true;
      } catch (e) { /* not JSON after all: shown as it came */ }
    }
    var pretty = isJson ? JSON.stringify(value, null, 2) : null;

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
      class: 'hlibz-tryit-copy',
      text: 'Copy',
      onclick: function () {
        copyText(isJson && showPretty ? pretty : text).then(
          function () { flash('Copied'); },
          function () { flash('Copy failed'); });
      },
    });
    var flashTimer = 0;
    function flash(message) {
      copy.textContent = message;
      clearTimeout(flashTimer);
      flashTimer = setTimeout(function () { copy.textContent = 'Copy'; }, 1500);
    }

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

  function renderResponse(state, response, text, elapsed, url) {
    var type = response.headers.get('content-type') || '';
    var json = /[/+]json\b/i.test(type);

    var headers = [];
    response.headers.forEach(function (value, name) {
      headers.push(name + ': ' + value);
    });

    var tone = response.status >= 500 ? 'error' : response.status >= 400 ? 'warning' : 'success';
    state.response.replaceChildren(
      h('div', { class: 'hlibz-tryit-status' },
        h('span', { class: 'hlibz-tryit-badge hlibz-tryit-' + tone, text: String(response.status) }),
        h('span', { text: response.statusText || '' }),
        h('span', { class: 'hlibz-tryit-hint', text: Math.round(elapsed) + ' ms' })),
      h('p', { class: 'hlibz-tryit-hint hlibz-tryit-url', text: url.href }),
      h('details', { class: 'hlibz-tryit-headers' },
        h('summary', { text: 'Headers (' + headers.length + ')' }),
        h('pre', { class: 'hlibz-tryit-code', text: headers.join('\n') })),
      text
        ? responseBody(text, json)
        : h('p', { class: 'hlibz-tryit-hint', text: 'No body.' }));
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
      var target = parseSectionId(section.getAttribute('data-section-id'));
      var entry = target && operationFor(target.path, target.method);
      if (!entry || entry.op['x-hideTryItPanel'] === true) {
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
    return response.json();
  }).then(function (document_) {
    spec = document_;
    mount();
    // ReDoc renders operations as they come near the viewport, so new ones keep arriving.
    new MutationObserver(scheduleMount).observe(document.body, { childList: true, subtree: true });
  }).catch(function (error) {
    // The page itself still works; only the console is missing.
    console.warn('Try it: could not read the OpenAPI document (' + error.message + ').');
  });
})();
