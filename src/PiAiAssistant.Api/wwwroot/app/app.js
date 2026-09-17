let conversationId = null;
let chart;

const messagesEl = document.getElementById("messages");
const chatForm = document.getElementById("chatForm");
const chatInput = document.getElementById("chatInput");
const loadChartBtn = document.getElementById("loadChartBtn");
const tagInput = document.getElementById("tagInput");
const hoursSelect = document.getElementById("hoursSelect");
const vizMeta = document.getElementById("vizMeta");

function addMessage(role, text, tools) {
  const div = document.createElement("div");
  div.className = `msg ${role}`;
  div.textContent = text;
  if (tools?.length) {
    const t = document.createElement("div");
    t.className = "tools";
    t.textContent = "Tools: " + tools.map(x => `${x.tool} (${x.status}, ${x.durationMs}ms)`).join(", ");
    div.appendChild(t);
  }
  messagesEl.appendChild(div);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function setPill(id, ok, label) {
  const el = document.getElementById(id);
  el.textContent = label;
  el.className = `pill ${ok ? "ok" : "bad"}`;
}

async function refreshHealth() {
  try {
    const pi = await fetch("/health/pi").then(r => r.json());
    setPill("piPill", !!pi.connected, pi.demoMode ? "PI: demo" : (pi.connected ? "PI: live" : "PI: down"));
  } catch {
    setPill("piPill", false, "PI: error");
  }
  try {
    const o = await fetch("/health/ollama").then(r => r.json());
    setPill("ollamaPill", !!o.reachable, o.reachable ? `Ollama: ${o.model}` : "Ollama: offline");
  } catch {
    setPill("ollamaPill", false, "Ollama: error");
  }
}

function renderVisualization(payload) {
  if (!payload?.series?.length) return;
  const ctx = document.getElementById("trendChart");
  const datasets = payload.series.map((s, i) => ({
    label: `${s.name}${s.unit ? ` (${s.unit})` : ""}`,
    data: s.points.map(p => ({ x: p.timestampUtc, y: p.value })),
    borderColor: ["#3d9cf0", "#3ecf8e", "#e6b84d", "#f07178", "#b48ead"][i % 5],
    tension: 0.2,
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
        x: {
          type: "time",
          time: { tooltipFormat: "yyyy-MM-dd HH:mm:ss" },
          ticks: { color: "#9aa8b5" },
          grid: { color: "#243040" }
        },
        y: {
          ticks: { color: "#9aa8b5" },
          grid: { color: "#243040" }
        }
      },
      plugins: {
        legend: { labels: { color: "#e8eef4" } },
        title: { display: !!payload.title, text: payload.title || "", color: "#e8eef4" }
      }
    }
  });
  vizMeta.textContent = JSON.stringify({
    title: payload.title,
    series: payload.series.map(s => ({ name: s.name, unit: s.unit, points: s.points.length }))
  }, null, 2);
}

// Chart.js time scale needs adapter — use category fallback if adapter missing
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
      body: JSON.stringify({ message, conversationId })
    });
    const data = await res.json();
    conversationId = data.conversationId;
    addMessage("bot", data.answer || "(empty)", data.toolTrace);
    if (data.visualization) {
      await ensureTimeScale();
      renderVisualization(data.visualization);
    }
  } catch (err) {
    addMessage("bot", `Request failed: ${err.message}`);
  } finally {
    btn.disabled = false;
  }
});

loadChartBtn.addEventListener("click", async () => {
  const tag = tagInput.value.trim();
  const hours = Number(hoursSelect.value || 24);
  if (!tag) return;
  loadChartBtn.disabled = true;
  try {
    const end = new Date();
    const start = new Date(end.getTime() - hours * 3600 * 1000);
    const url = `/api/tags/chart?reference=${encodeURIComponent(tag)}&startUtc=${encodeURIComponent(start.toISOString())}&endUtc=${encodeURIComponent(end.toISOString())}&maxCount=300`;
    const res = await fetch(url);
    const data = await res.json();
    if (!res.ok || data.status !== 0 && data.status !== "Found" && data.Status !== 0) {
      // enum may serialize as number 0 = Found
      if (data.status !== 0 && data.Status !== undefined && data.Status !== 0) {
        vizMeta.textContent = JSON.stringify(data, null, 2);
        return;
      }
    }
    const status = data.status ?? data.Status;
    if (status !== 0 && status !== "Found") {
      vizMeta.textContent = JSON.stringify(data, null, 2);
      return;
    }
    await ensureTimeScale();
    renderVisualization({
      chartType: "line",
      title: data.canonicalName || data.CanonicalName,
      series: [{
        name: data.canonicalName || data.CanonicalName,
        unit: data.unit || data.Unit,
        points: (data.points || data.Points || []).map(p => ({
          timestampUtc: p.timestampUtc || p.TimestampUtc,
          value: p.value ?? p.Value
        }))
      }]
    });
  } catch (err) {
    vizMeta.textContent = err.message;
  } finally {
    loadChartBtn.disabled = false;
  }
});

addMessage("bot", "Ask about any PI tag — current value, specs, history, summary, related signals, or compare tags. Charts render when trend tools are used.");
refreshHealth();
ensureTimeScale().then(() => loadChartBtn.click());
