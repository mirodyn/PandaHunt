// Moves the panda around the viewport. It walks in a straight line until it hits an edge,
// then picks a new random direction away from that edge (and sometimes sits down for a bit).

const SPEED = 90;              // px per second
const REST_CHANCE = 0.3;       // chance to rest after touching an edge
const REST_MIN_MS = 1200;
const REST_EXTRA_MS = 2000;

export function start(el) {
    const maxX = () => Math.max(0, window.innerWidth - el.offsetWidth);
    const maxY = () => Math.max(0, window.innerHeight - el.offsetHeight);

    let x = Math.random() * maxX();
    let y = Math.random() * maxY();
    let vx = 0, vy = 0;
    let restUntil = 0;
    let last = performance.now();
    let frameId = 0;

    const place = () => { el.style.transform = `translate(${x}px, ${y}px)`; };

    // Respect users who prefer no motion: park the panda in the bottom-right corner.
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        x = maxX(); y = maxY();
        place();
        el.dataset.state = "resting";
        return { stop() { } };
    }

    // New heading within ±60° of the direction pointing away from the wall (normal nx, ny).
    function turnAway(nx, ny) {
        const angle = Math.atan2(ny, nx) + (Math.random() - 0.5) * (Math.PI * 2 / 3);
        vx = Math.cos(angle) * SPEED;
        vy = Math.sin(angle) * SPEED;
        el.style.setProperty("--look-x", (vx / SPEED).toFixed(2));
        el.style.setProperty("--look-y", (vy / SPEED).toFixed(2));
    }

    function frame(now) {
        const dt = Math.min((now - last) / 1000, 0.1); // clamp so a background tab doesn't teleport it
        last = now;

        if (now >= restUntil) {
            el.dataset.state = "walking";
            x += vx * dt;
            y += vy * dt;

            let nx = 0, ny = 0;
            if (x <= 0) { x = 0; nx = 1; } else if (x >= maxX()) { x = maxX(); nx = -1; }
            if (y <= 0) { y = 0; ny = 1; } else if (y >= maxY()) { y = maxY(); ny = -1; }

            if (nx !== 0 || ny !== 0) {
                turnAway(nx, ny);
                if (Math.random() < REST_CHANCE) {
                    restUntil = now + REST_MIN_MS + Math.random() * REST_EXTRA_MS;
                    el.dataset.state = "resting";
                }
            }
        }

        place();
        frameId = requestAnimationFrame(frame);
    }

    turnAway(Math.random() - 0.5, Math.random() - 0.5);
    place();
    frameId = requestAnimationFrame(frame);

    return {
        stop() { cancelAnimationFrame(frameId); }
    };
}
