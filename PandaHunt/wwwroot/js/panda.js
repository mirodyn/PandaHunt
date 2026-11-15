// Moves the panda around the viewport. It walks in a straight line until it hits an edge,
// then picks a new random direction away from that edge (and sometimes sits down for a bit).
//
// Feeding minigame: clicking an empty spot grows a bamboo there (one at a time). The panda heads
// straight for it, eats it and tells the server (WanderingPanda.BambooEaten), which answers how many
// of the 5 dots above her head are green. The dots only pop up for a moment after each bamboo.
// A pause longer than 30 s between bamboos starts over.
//
// Talking: clicking on the panda herself shows a speech bubble with whatever the server picked
// (WanderingPanda.PandaClicked, texts in Services/PandaTalk.cs). Bamboo never grows on or right next to her.

const SPEED = 90;              // px per second, wandering
const CHASE_SPEED = 230;       // px per second, on the way to a bamboo
const REST_CHANCE = 0.3;       // chance to rest after touching an edge
const REST_MIN_MS = 1200;
const REST_EXTRA_MS = 2000;

const EAT_MS = 1300;           // keep in sync with the "eaten" animation in WanderingPanda.razor.css
const FEED_TIMEOUT_MS = 30000; // keep in sync with PandaFeeding.Timeout
const DOTS_SHOWN_MS = 1800;    // how long the dots stay up after each bamboo
const CELEBRATE_MS = 2500;     // ... and after the fifth one
const SPROUT_W = 44, SPROUT_H = 76; // .ph-sprout size in WanderingPanda.razor.css
const NO_PLANT_MARGIN = 30;    // px around the panda where clicks don't grow bamboo
const TALK_MS = 2600;          // how long a speech bubble stays

export function start(el, sprout, bubble, dotnet) {
    const body = el.querySelector(".ph-panda-svg");
    const bubbleText = bubble.querySelector(".ph-bubble-text");
    const dotsBox = el.querySelector(".ph-panda-dots");
    const dots = [...el.querySelectorAll(".ph-panda-dot")];
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const maxX = () => Math.max(0, window.innerWidth - el.offsetWidth);
    const maxY = () => Math.max(0, window.innerHeight - el.offsetHeight);
    const clamp = (v, min, max) => Math.min(Math.max(v, min), max);

    let x = Math.random() * maxX();
    let y = Math.random() * maxY();
    let vx = 0, vy = 0;
    let restUntil = 0;
    let last = performance.now();
    let frameId = 0;

    let mode = "wander";       // "wander" | "chase" | "eat"
    let target = null;         // where to stand to eat ({ x, y }), while chasing
    let eatUntil = 0;
    let bambooOut = false;     // only one bamboo at a time
    let fed = 0, lastFedAt = 0, celebrating = false, hideTimer = 0;
    let talkTimer = 0;

    const place = () => { el.style.transform = `translate(${x}px, ${y}px)`; };

    function look(dx, dy) {
        const length = Math.hypot(dx, dy) || 1;
        el.style.setProperty("--look-x", (dx / length).toFixed(2));
        el.style.setProperty("--look-y", (dy / length).toFixed(2));
    }

    // New heading within ±60° of the direction pointing away from the wall (normal nx, ny).
    function turnAway(nx, ny) {
        const angle = Math.atan2(ny, nx) + (Math.random() - 0.5) * (Math.PI * 2 / 3);
        vx = Math.cos(angle) * SPEED;
        vy = Math.sin(angle) * SPEED;
        look(vx, vy);
    }

    // ---- Feeding -------------------------------------------------------------------------------

    // "Empty space": the page background or the home page's own padding, never text or controls.
    function isEmptySpot(target) {
        return target === document.body
            || target === document.documentElement
            || (target instanceof Element && target.hasAttribute("data-feed-zone"));
    }

    // On her body: an ellipse inside her picture (the corners of the picture are empty).
    function isOnPanda(px, py) {
        const r = body.getBoundingClientRect();
        const nx = (px - (r.left + r.right) / 2) / (r.width / 2);
        const ny = (py - (r.top + r.bottom) / 2) / (r.height / 2);
        return nx * nx + ny * ny <= 1;
    }

    function isNearPanda(px, py) {
        const r = body.getBoundingClientRect();
        return px > r.left - NO_PLANT_MARGIN && px < r.right + NO_PLANT_MARGIN
            && py > r.top - NO_PLANT_MARGIN && py < r.bottom + NO_PLANT_MARGIN;
    }

    function onClick(e) {
        if (e.button !== 0 || !isEmptySpot(e.target))
            return;
        if (isOnPanda(e.clientX, e.clientY)) {
            talk();
            return;
        }
        if (bambooOut || isNearPanda(e.clientX, e.clientY))
            return;

        // The bamboo stands on the clicked spot (bottom centre), kept fully on screen.
        const cx = clamp(e.clientX, SPROUT_W / 2, window.innerWidth - SPROUT_W / 2);
        const cy = clamp(e.clientY, SPROUT_H, window.innerHeight);
        sprout.style.transform = `translate(${cx - SPROUT_W / 2}px, ${cy - SPROUT_H}px)`;
        sprout.dataset.state = "growing";
        bambooOut = true;

        // Stand so the panda's mouth (half-way across, about half-way down) is at the middle of the stalk.
        const w = el.offsetWidth;
        target = {
            x: clamp(cx - w * 0.5, 0, maxX()),
            y: clamp(cy - SPROUT_H / 2 - w * 0.52, 0, maxY()),
        };
        mode = "chase";
        restUntil = 0;
    }

    function startEating(now) {
        mode = "eat";
        target = null;
        eatUntil = now + (reducedMotion ? 300 : EAT_MS);
        el.dataset.state = "eating";
        sprout.dataset.state = "eaten";
    }

    async function finishEating() {
        sprout.removeAttribute("data-state");
        bambooOut = false;
        mode = "wander";
        turnAway(Math.random() - 0.5, Math.random() - 0.5);

        try {
            const state = await dotnet.invokeMethodAsync("BambooEaten");
            showFed(state.count, state.goal);
        } catch {
            // Lost the connection to the server; the page will reconnect or reload.
        }
    }

    // Pops the dots up for a moment with the current progress, then fades them out again.
    function showFed(count, goal) {
        clearTimeout(hideTimer);
        fed = count;
        lastFedAt = performance.now();
        celebrating = count >= goal;
        dots.forEach((dot, i) => dot.classList.toggle("on", i < count));
        dotsBox.classList.toggle("done", celebrating);
        dotsBox.classList.toggle("show", count > 0);

        hideTimer = celebrating
            ? setTimeout(resetFed, CELEBRATE_MS)
            : setTimeout(() => dotsBox.classList.remove("show"), DOTS_SHOWN_MS);
    }

    function resetFed() {
        clearTimeout(hideTimer);
        fed = 0;
        celebrating = false;
        dotsBox.classList.remove("show", "done");
        dots.forEach(dot => dot.classList.remove("on"));
    }

    // ---- Talking -------------------------------------------------------------------------------

    async function talk() {
        let reply;
        try {
            reply = await dotnet.invokeMethodAsync("PandaClicked");
        } catch {
            return; // lost the connection to the server
        }

        bubbleText.textContent = reply.text;
        bubble.classList.toggle("annoyed", reply.annoyed);
        // Restart the pop-in animation even if a bubble is already showing.
        bubble.classList.remove("show");
        void bubble.offsetWidth;
        bubble.classList.add("show");
        placeBubble();

        clearTimeout(talkTimer);
        talkTimer = setTimeout(() => bubble.classList.remove("show"), TALK_MS);

        // Stop and look at whoever clicked (unless she's busy with a bamboo).
        if (mode === "wander") {
            restUntil = performance.now() + TALK_MS;
            el.dataset.state = "resting";
            el.style.setProperty("--look-x", "0");
            el.style.setProperty("--look-y", "0.6");
        }
    }

    // Above her head (or below her feet near the top of the screen), kept inside the screen;
    // the little tail still points at her head.
    function placeBubble() {
        const bw = bubble.offsetWidth, bh = bubble.offsetHeight;
        const headX = x + el.offsetWidth / 2;
        const left = clamp(headX - bw / 2, 8, Math.max(8, window.innerWidth - bw - 8));
        const below = y - bh - 30 < 8;
        const top = below ? y + el.offsetHeight + 14 : y - bh - 30;
        bubble.style.transform = `translate(${left}px, ${top}px)`;
        bubble.style.setProperty("--tail-x", `${clamp(headX - left, 14, bw - 14)}px`);
        bubble.classList.toggle("below", below);
    }

    // ---- Main loop -----------------------------------------------------------------------------

    function frame(now) {
        const dt = Math.min((now - last) / 1000, 0.1); // clamp so a background tab doesn't teleport it
        last = now;

        if (mode === "chase" && target) {
            const dx = target.x - x, dy = target.y - y;
            const distance = Math.hypot(dx, dy);
            const step = reducedMotion ? Infinity : CHASE_SPEED * dt;
            el.dataset.state = "walking";
            look(dx, dy);
            if (distance <= step) {
                x = target.x; y = target.y;
                startEating(now);
            } else {
                x += dx / distance * step;
                y += dy / distance * step;
            }
        } else if (mode === "eat") {
            if (now >= eatUntil)
                finishEating();
        } else if (reducedMotion) {
            el.dataset.state = "resting"; // no wandering: she only moves to fetch a bamboo
        } else if (now >= restUntil) {
            if (el.dataset.state !== "walking")
                look(vx, vy); // after a pause, look where she's going again
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

        // Too long since the last bamboo: start over.
        if (fed > 0 && !celebrating && now - lastFedAt > FEED_TIMEOUT_MS)
            resetFed();

        place();
        if (bubble.classList.contains("show"))
            placeBubble(); // follows her if she's walking to a bamboo
        frameId = requestAnimationFrame(frame);
    }

    if (reducedMotion) {
        // Park in the bottom-right corner.
        x = maxX(); y = maxY();
        el.dataset.state = "resting";
    } else {
        turnAway(Math.random() - 0.5, Math.random() - 0.5);
    }

    document.addEventListener("click", onClick);
    place();
    frameId = requestAnimationFrame(frame);

    return {
        stop() {
            cancelAnimationFrame(frameId);
            clearTimeout(hideTimer);
            clearTimeout(talkTimer);
            document.removeEventListener("click", onClick);
        }
    };
}
