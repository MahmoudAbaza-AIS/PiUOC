let conversationId = null;
let chart;
let currentLevel = "kingdom";

const messagesEl = document.getElementById("messages");
const chatForm = document.getElementById("chatForm");
const chatInput = document.getElementById("chatInput");
const modelSelect = document.getElementById("modelSelect");
const personaSelect = document.getElementById("personaSelect");
const langSelect = document.getElementById("langSelect");
const kpiTiles = document.getElementById("kpiTiles");
const screenBody = document.getElementById("screenBody");
const screenTitle = document.getElementById("screenTitle");
const crumbs = document.getElementById("crumbs");
const briefCard = document.getElementById("briefCard");
const promptChips = document.getElementById("promptChips");
const vizMeta = document.getElementById("vizMeta");

const promptsByPersona = {
  Executive: [
    "How are we running today vs. yesterday?",
    "Which sector has the largest generation decline?"
  ],
  SectorOps: [
    "Which plants in COA are below target loading?",
    "أي محطات في COA أقل من هدف التحميل؟"
  ],
  PlantManager: [
    "Why is PP09 heat rate worse than last week?",
    "Which PP09 blocks account for today’s MW shortfall?"
  ],
  ShiftOperator: [
    "Flame intensity says bad on GT01 — what do I do?",
    "What changed on GT01 since the start of my shift?"
  ],
  ReliabilityEngineer: [
    "Show me units trending outside their baseline over 30 days",
    "Do comparable units show the same heat-rate trend?"
  ]
};

const levelToPersona = {
  kingdom: "Executive",
  sector: "SectorOps",
  plant: "PlantManager",
  unit: "ShiftOperator"
};

function addMessage(role, text, extras) {
  const div = document.createElement("div");
  div.className = `msg ${role}`;
  div.textContent = text;
  if (extras?.persona || extras?.action || extras?.model) {
    const m = document.createElement("div");
    m.className = "tools";
    const bits = [];
    if (extras.persona) bits.push(`Persona: ${extras.persona}`);
    if (extras.model) bits.push(`Model: ${extras.model}`);
    if (extras.action) bits.push(`Action: ${extras.action}`);
    if (extras.jump) bits.push(`Jump: ${extras.jump}`);
    if (extras.tools?.length) {
      bits.push("Tools: " + extras.tools.map(x => `${x.tool} (${x.status})`).join(", "));
    }
    m.textContent = bits.join(" · ");
    div.appendChild(m);
  }
  messagesEl.appendChild(div);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function setPill(id, ok, label) {
  const el = document.getElementById(id);
  el.textContent = label;
  el.className = `pill ${ok ? "ok" : "bad"}`;
}

function renderKpis(items) {
  kpiTiles.innerHTML = "";
  (items || []).forEach(k => {
    const el = document.createElement("div");
    el.className = "kpi";
    el.innerHTML = `<div class="label">${k.displayName || k.key}</div>
      <div class="value">${formatVal(k.value)}<span class="unit">${k.unit || ""}</span></div>`;
    kpiTiles.appendChild(el);
  });
}

function formatVal(v) {
  if (typeof v === "number") return Number.isInteger(v) ? v.toLocaleString() : v.toLocaleString(undefined, { maximumFractionDigits: 2 });
  return v ?? "—";
}

function renderPrompts() {
  const list = promptsByPersona[personaSelect.value] || [];
  promptChips.innerHTML = "";
  list.forEach(p => {
    const b = document.createElement("button");
    b.type = "button";
    b.textContent = p;
    b.onclick = () => { chatInput.value = p; chatInput.focus(); };
    promptChips.appendChild(b);
  });
}

async function loadModels() {
  try {
    const data = await fetch("/api/chat/models").then(r => r.json());
    if (!data?.models?.length) return;
    modelSelect.innerHTML = "";
    for (const m of data.models) {
      const opt = document.createElement("option");
      opt.value = m.key;
      opt.textContent = `${m.key} (${m.modelId})`;
      if (m.isDefault) opt.selected = true;
      modelSelect.appendChild(opt);
    }
  } catch { /* keep defaults */ }
}

async function refreshHealth() {
  try {
    const pi = await fetch("/health/pi").then(r => r.json());
    const source = pi.source || (pi.demoMode ? "demo" : "live");
    const label = !pi.connected
      ? "PI: down"
      : source === "simulator"
        ? "PI: simulator"
        : source === "demo-fallback"
          ? "PI: demo (sim down)"
          : pi.demoMode
            ? "PI: demo"
            : "PI: live";
    setPill("piPill", !!pi.connected, label);
  } catch {
    setPill("piPill", false, "PI: error");
  }
  try {
    const o = await fetch("/health/ollama").then(r => r.json());
    setPill("ollamaPill", !!o.reachable, o.reachable ? `Ollama: ${o.model || "up"}` : "Ollama: offline (deterministic OK)");
  } catch {
    setPill("ollamaPill", false, "Ollama: offline (deterministic OK)");
  }
}

function renderVisualization(payload) {
  if (!payload?.series?.length) {
    vizMeta.textContent = "";
    return;
  }
  const ctx = document.getElementById("trendChart");
  const datasets = payload.series.map((s, i) => ({
    label: `${s.name}${s.unit ? ` (${s.unit})` : ""}`,
    data: s.points.map(p => ({ x: p.timestampUtc, y: p.value })),
    borderColor: ["#2ec4ff", "#3ecf8e", "#e6b84d", "#f07178"][i % 4],
    tension: 0.15,
    pointRadius: 0,
    borderWidth: 2
  }));
  if (chart) chart.destroy();
  chart = new Chart(ctx, {
    type: "line",
    data: { datasets },
    options: {
      responsive: true,
      parsing: false,
      scales: {
        x: { type: "time", ticks: { color: "#8fa3bc" }, grid: { color: "#1c2e44" } },
        y: { ticks: { color: "#8fa3bc" }, grid: { color: "#1c2e44" } }
      },
      plugins: {
        legend: { labels: { color: "#e7f1ff" } },
        title: { display: !!payload.title, text: payload.title || "", color: "#e7f1ff" }
      }
    }
  });
  vizMeta.textContent = JSON.stringify({
    title: payload.title,
    series: payload.series.map(s => ({ name: s.name, points: s.points.length }))
  }, null, 2);
}

async function ensureTimeScale() {
  if (Chart._adapters?._date?.formats) return;
  await new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://cdn.jsdelivr.net/npm/chartjs-adapter-date-fns@3.0.0/dist/chartjs-adapter-date-fns.bundle.min.js";
    s.onload = resolve;
    s.onerror = reject;
    document.head.appendChild(s);
  });
}

async function loadScreen() {
  const level = currentLevel;
  if (level === "kingdom") {
    const data = await fetch("/api/fleet/kingdom").then(r => r.json());
    screenTitle.textContent = "Kingdom overview";
    crumbs.textContent = "Kingdom › KSA";
    renderKpis([
      { displayName: "Gross Power", value: data.kingdom.grossMw, unit: "MW" },
      { displayName: "Net Power", value: data.kingdom.netMw, unit: "MW" },
      { displayName: "Loading Factor", value: data.kingdom.loadingFactorPercent, unit: "%" },
      { displayName: "CO₂", value: data.kingdom.co2TonPerDay, unit: "t/day" }
    ]);
    screenBody.innerHTML = `<p>${data.narrativeEn}</p>
      <p class="warn">Flagged sector: <strong>${data.flaggedSectorCode}</strong></p>
      <p>Next: ${data.recommendedAction}</p>
      <div>${data.sectors.map(s => `<div class="rank">${s.code}: ${s.grossMw.toLocaleString()} MW · LF ${s.loadingFactorPercent}% · IS ${s.inServiceUnits}/${s.totalUnits}</div>`).join("")}</div>`;
    renderVisualization(null);
  } else if (level === "sector") {
    const data = await fetch("/api/fleet/sectors/COA").then(r => r.json());
    screenTitle.textContent = "Sector COA";
    crumbs.textContent = "Kingdom › COA";
    renderKpis([
      { displayName: "Gross Power", value: data.sector.grossMw, unit: "MW" },
      { displayName: "Capacity", value: data.sector.capacityMw, unit: "MW" },
      { displayName: "Loading Factor", value: data.sector.loadingFactorPercent, unit: "%" },
      { displayName: "In-Service", value: `${data.sector.inServiceUnits}/${data.sector.totalUnits}` }
    ]);
    screenBody.innerHTML = `<p>${data.narrativeEn}</p>
      <h3 style="color:#8fa3bc;font-size:0.85rem;">Plants below target</h3>
      ${data.plantsBelowTarget.map(p => `<div class="rank"><strong>${p.plantCode}</strong> loading ${p.loadingPercent}% (target ${p.targetLoadingPercent}%) · Δ ${p.deltaVsTarget.toFixed(1)} · ${p.visionJumpPath}</div>`).join("") || "<p>None</p>"}`;
  } else if (level === "plant") {
    const data = await fetch("/api/fleet/plants/PP09").then(r => r.json());
    screenTitle.textContent = "Plant PP09";
    crumbs.textContent = "Kingdom › COA › PP09";
    renderKpis([
      { displayName: "Gross Power", value: data.plant.grossMw, unit: "MW" },
      { displayName: "Capacity", value: data.plant.capacityMw, unit: "MW" },
      { displayName: "Loading", value: data.plant.loadingFactorPercent, unit: "%" },
      { displayName: "Heat rate Δ", value: data.plant.heatRateDeltaWeekPercent, unit: "%" }
    ]);
    screenBody.innerHTML = `<p>${data.rootCauseNarrativeEn}</p>
      <p>SOP: ${data.sopCitation || "—"} · Next: ${data.recommendedAction}</p>
      <div>${data.blocks.map(b => `<div class="rank">${b.name}: ${b.grossMw} MW · fuel ${b.fuelKgPerDay ?? "n/a"} kg/day</div>`).join("")}</div>`;
    await ensureTimeScale();
    renderVisualization({
      chartType: "line",
      title: "PP09 Gen MWh vs Fuel",
      series: [
        { name: "Gen MWh", unit: "MWh", points: data.genTrend },
        { name: "Fuel", unit: "kg", points: data.fuelTrend }
      ]
    });
  } else {
    const data = await fetch("/api/fleet/plants/PP09/blocks/A1/units/GT01").then(r => r.json());
    screenTitle.textContent = "Unit Block A1 GT01";
    crumbs.textContent = "Kingdom › COA › PP09 › Block A1 › GT01";
    renderKpis([
      { displayName: "Active Power", value: data.unit.activePowerMw, unit: "MW" },
      { displayName: "Loading", value: data.unit.loadingPercent, unit: "%" },
      { displayName: "Fuel Flow", value: data.unit.fuelFlowKgPerSec, unit: "kg/s" },
      { displayName: "Gen MWh/Day", value: data.unit.genMwhDay, unit: "MWh" }
    ]);
    screenBody.innerHTML = `<p>${data.narrativeEn}</p>
      <p class="bad">Likely cause: ${data.likelyCause}</p>
      <p>SOP: ${data.sopCitation || "—"}</p>
      <p>Next: ${data.recommendedAction}</p>
      <p>Escalate: ${data.escalationContact}</p>
      <div class="rank">Flame: ${data.unit.flameIntensityStatus.join(", ")}</div>`;
  }
}

async function loadBrief() {
  const persona = personaSelect.value;
  const lang = langSelect.value;
  let url = `/api/briefings/executive/morning?lang=${lang}`;
  if (persona === "SectorOps") url = `/api/briefings/sector/COA?lang=${lang}`;
  if (persona === "PlantManager") url = `/api/briefings/plants/PP09?lang=${lang}`;
  if (persona === "ShiftOperator") url = `/api/briefings/alarms/PP09/A1/GT01?lang=${lang}`;
  if (persona === "ReliabilityEngineer") url = `/api/briefings/demo?lang=${lang}`;

  const data = await fetch(url).then(r => r.json());
  const brief = Array.isArray(data) ? data.find(b => b.persona === "ReliabilityEngineer") || data[0] : data;
  briefCard.innerHTML = `<div class="title">${brief.title}</div>${brief.narrative}
    <div class="meta-line">Trigger: ${brief.trigger} · Action: ${brief.recommendedAction || "—"} · ${brief.visionJumpPath || ""}</div>`;
}

document.getElementById("levelNav").addEventListener("click", async (e) => {
  const btn = e.target.closest("button[data-level]");
  if (!btn) return;
  currentLevel = btn.dataset.level;
  document.querySelectorAll("#levelNav button").forEach(b => b.classList.toggle("active", b === btn));
  personaSelect.value = levelToPersona[currentLevel] || "Executive";
  renderPrompts();
  await loadScreen();
});

personaSelect.addEventListener("change", renderPrompts);
document.getElementById("refreshScreenBtn").addEventListener("click", loadScreen);
document.getElementById("loadBriefBtn").addEventListener("click", loadBrief);

chatForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  const message = chatInput.value.trim();
  if (!message) return;
  addMessage("user", message);
  chatInput.value = "";
  const btn = chatForm.querySelector("button");
  btn.disabled = true;
  try {
    const res = await fetch("/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        message,
        conversationId,
        model: modelSelect?.value || null,
        persona: personaSelect.value,
        language: langSelect.value,
        screenLevel: currentLevel
      })
    });
    const data = await res.json();
    conversationId = data.conversationId;
    addMessage("bot", data.answer || "(empty)", {
      persona: data.persona,
      model: data.model || (data.usedDeterministicFallback ? "deterministic" : null),
      action: data.recommendedAction,
      jump: data.visionJumpPath,
      tools: data.toolTrace
    });
    if (data.kpis?.length) renderKpis(data.kpis);
    if (data.visualization) {
      await ensureTimeScale();
      renderVisualization(data.visualization);
    }
  } catch (err) {
    addMessage("bot", String(err));
  } finally {
    btn.disabled = false;
  }
});

renderPrompts();
loadModels();
refreshHealth();
loadScreen();
loadBrief();
setInterval(refreshHealth, 30000);
