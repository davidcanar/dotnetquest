// Tiny dependency-free canvas confetti for celebrations.

const COLORS = ['#22c55e', '#facc15', '#38bdf8', '#f472b6', '#a78bfa', '#fb923c'];
let canvas = null;
let ctx = null;
let particles = [];
let rafId = null;

function ensureCanvas() {
    if (canvas) return true;
    canvas = document.createElement('canvas');
    canvas.style.position = 'fixed';
    canvas.style.inset = '0';
    canvas.style.width = '100%';
    canvas.style.height = '100%';
    canvas.style.pointerEvents = 'none';
    canvas.style.zIndex = '9999';
    document.body.appendChild(canvas);
    ctx = canvas.getContext('2d');
    resize();
    window.addEventListener('resize', resize);
    return true;
}

function resize() {
    if (!canvas) return;
    canvas.width = window.innerWidth * (window.devicePixelRatio || 1);
    canvas.height = window.innerHeight * (window.devicePixelRatio || 1);
}

function spawn(count) {
    const dpr = window.devicePixelRatio || 1;
    for (let i = 0; i < count; i++) {
        particles.push({
            x: canvas.width / 2,
            y: canvas.height * 0.35,
            vx: (Math.random() - 0.5) * 14 * dpr,
            vy: (Math.random() * -12 - 4) * dpr,
            size: (4 + Math.random() * 6) * dpr,
            color: COLORS[Math.floor(Math.random() * COLORS.length)],
            rotation: Math.random() * Math.PI * 2,
            vr: (Math.random() - 0.5) * 0.3,
            life: 90 + Math.random() * 60
        });
    }
}

function tick() {
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    particles = particles.filter(p => p.life > 0);
    for (const p of particles) {
        p.vy += 0.35 * (window.devicePixelRatio || 1);
        p.x += p.vx;
        p.y += p.vy;
        p.rotation += p.vr;
        p.life -= 1;
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(p.rotation);
        ctx.fillStyle = p.color;
        ctx.globalAlpha = Math.min(1, p.life / 30);
        ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
        ctx.restore();
    }
    if (particles.length > 0) {
        rafId = requestAnimationFrame(tick);
    } else {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        rafId = null;
    }
}

export function burst(count = 120) {
    if (!ensureCanvas()) return;
    spawn(count);
    if (rafId === null) rafId = requestAnimationFrame(tick);
}
