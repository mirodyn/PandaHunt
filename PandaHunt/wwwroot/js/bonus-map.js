// Bonus events: maps (Leaflet + OpenStreetMap, loaded from a CDN only when a map is shown)
// and asking the browser where the player is.

const LEAFLET = "https://unpkg.com/leaflet@1.9.4/dist/";
const LEAFLET_JS_SRI = "sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=";
const LEAFLET_CSS_SRI = "sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=";
const PRAGUE = [50.0875, 14.4214]; // where the map starts before a place is picked

let leafletLoading = null;

function load(tag, attributes) {
    return new Promise((resolve, reject) => {
        const element = Object.assign(document.createElement(tag), attributes, { crossOrigin: "" });
        element.onload = resolve;
        element.onerror = () => { element.remove(); reject(new Error(`Could not load ${attributes.src ?? attributes.href}`)); };
        document.head.appendChild(element);
    });
}

function leaflet() {
    if (window.L)
        return Promise.resolve(window.L);
    leafletLoading ??= Promise.all([
        load("link", { rel: "stylesheet", href: LEAFLET + "leaflet.css", integrity: LEAFLET_CSS_SRI }),
        load("script", { src: LEAFLET + "leaflet.js", integrity: LEAFLET_JS_SRI }),
    ]).then(() => window.L, error => { leafletLoading = null; throw error; });
    return leafletLoading;
}

// A view is set straight away: circles can only measure themselves on a map that has one.
function createMap(L, el, center, zoom, options) {
    const map = L.map(el, options).setView(center, zoom);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a>',
    }).addTo(map);
    // Dialogs and phones change the size after the map is created.
    const resize = new ResizeObserver(() => map.invalidateSize());
    resize.observe(el);
    return { map, dispose() { resize.disconnect(); map.remove(); } };
}

// Black map pin (styled in app.css): a rotated square whose sharp corner (13·√2 below the centre) marks the spot.
const pin = L => L.divIcon({ className: "ph-map-pin", html: "<span></span>", iconSize: [26, 26], iconAnchor: [13, 31] });

const areaStyle = { color: "#000", weight: 2, fillColor: "#000", fillOpacity: 0.12 };

// ---- Player: show the event's place --------------------------------------------------------------

export async function showPlace(el, lat, lng, radius) {
    const L = await leaflet();
    const { map, dispose } = createMap(L, el, [lat, lng], 16, { scrollWheelZoom: false });
    const area = L.circle([lat, lng], { ...areaStyle, radius, interactive: false }).addTo(map);
    L.marker([lat, lng], { icon: pin(L), interactive: false, keyboard: false }).addTo(map);
    map.fitBounds(area.getBounds(), { padding: [24, 24], maxZoom: 17 });

    let me = null;
    return {
        // The player's position (after a claim attempt), with how precise it is.
        showMe(myLat, myLng, accuracy) {
            me?.remove();
            me = L.layerGroup([
                L.circle([myLat, myLng], { radius: accuracy, color: "#000", weight: 1, dashArray: "4 4", fillOpacity: 0.04, interactive: false }),
                L.circleMarker([myLat, myLng], { radius: 7, color: "#000", weight: 3, fillColor: "#fff", fillOpacity: 1, interactive: false }),
            ]).addTo(map);
            map.fitBounds(area.getBounds().extend([myLat, myLng]), { padding: [24, 24], maxZoom: 17 });
        },
        dispose,
    };
}

// ---- Admin: pick the place -----------------------------------------------------------------------

// Click the map (or drag the pin) to put the event there; the circle shows the radius.
// Reports every change to dotnet.PlacePicked(lat, lng).
export async function pickPlace(el, lat, lng, radius, dotnet) {
    const L = await leaflet();
    const hasPlace = lat != null && lng != null;
    const { map, dispose } = createMap(L, el, hasPlace ? [lat, lng] : PRAGUE, hasPlace ? 16 : 12, {});
    let marker = null, area = null;

    function put(latLng) {
        if (marker) {
            marker.setLatLng(latLng);
            area.setLatLng(latLng);
            return;
        }
        area = L.circle(latLng, { ...areaStyle, radius, interactive: false }).addTo(map);
        marker = L.marker(latLng, { icon: pin(L), draggable: true, autoPan: true, title: "Přetáhni na správné místo" }).addTo(map);
        marker.on("drag", () => area.setLatLng(marker.getLatLng()));
        marker.on("dragend", () => report(marker.getLatLng().wrap()));
    }

    function report(latLng) {
        dotnet.invokeMethodAsync("PlacePicked", latLng.lat, latLng.lng).catch(() => { /* dialog closed */ });
    }

    map.on("click", e => {
        const latLng = e.latlng.wrap();
        put(latLng);
        report(latLng);
    });

    if (hasPlace) {
        put([lat, lng]);
        map.fitBounds(area.getBounds(), { padding: [24, 24], maxZoom: 17 });
    }

    return {
        // Coordinates typed in by hand, or the admin's own position.
        setPlace(newLat, newLng) {
            put([newLat, newLng]);
            map.fitBounds(area.getBounds(), { padding: [24, 24], maxZoom: Math.max(map.getZoom(), 16) });
        },
        setRadius(newRadius) {
            radius = newRadius;
            area?.setRadius(newRadius);
        },
        dispose,
    };
}

// ---- Where am I? ---------------------------------------------------------------------------------

// One fresh position from the browser (it asks the player first). Never rejects:
// { latitude, longitude, accuracy } or { error: "denied" | "unavailable" | "timeout" | "unsupported" }.
export function locate() {
    return new Promise(resolve => {
        if (!window.isSecureContext || !("geolocation" in navigator)) {
            resolve({ error: "unsupported" });
            return;
        }
        navigator.geolocation.getCurrentPosition(
            p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracy: p.coords.accuracy }),
            e => resolve({ error: e.code === e.PERMISSION_DENIED ? "denied" : e.code === e.TIMEOUT ? "timeout" : "unavailable" }),
            { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 });
    });
}

// "granted" | "prompt" | "denied" | "unknown" (older browsers can't tell before asking).
export async function locationPermission() {
    try {
        return (await navigator.permissions.query({ name: "geolocation" })).state;
    } catch {
        return "unknown";
    }
}
