let conversationId = null;
let chart;
let sectorCharts = [];
let sectorChartTries = 0;
let currentLevel = "sector";

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
    "What's the top-performing plant in COA?",
    "Show me the sector breakdown",
    "What's the loading factor and in-service count?",
    "Which plants have unhealthy data?",
    "What are yesterday's generation and fuel figures?"
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

const promptsByPersonaAr = {
  SectorOps: [
    "ما أفضل محطة أداءً في COA؟",
    "أرني توزيع القطاع",
    "ما معامل التحميل وعدد الوحدات في الخدمة؟",
    "أي المحطات بياناتها غير سليمة؟",
    "ما أرقام التوليد والوقود لأمس؟"
  ]
};

const chromeText = {
  en: {
    assistantTitle: "Decision assistant",
    personaLabel: "Persona",
    modelLabel: "Model",
    personaExecutive: "VP / Executive",
    personaSectorOps: "Sector Ops",
    personaPlantManager: "Plant Manager",
    personaShiftOperator: "Shift Operator",
    personaReliability: "Reliability",
    ask: "Ask",
    askPlaceholder: "Ask about the COA board",
    hint: "POC mock — the same question always returns the same answer. No model call.",
    pushTitle: "Proactive push",
    loadBrief: "Load persona brief",
    briefEmpty: "Select a persona and load the scheduled / alarm push.",
    hide: "Hide",
    assistant: "Assistant",
    hideAssistant: "Hide assistant",
    showAssistant: "Show assistant",
    hidePush: "Hide proactive push",
    showPush: "Show proactive push",
    refresh: "Refresh",
    briefOffline: "Persona brief is unavailable offline. COA board answers do not need the API."
  },
  ar: {
    assistantTitle: "مساعد القرار",
    personaLabel: "الدور",
    modelLabel: "النموذج",
    personaExecutive: "نائب الرئيس / تنفيذي",
    personaSectorOps: "عمليات القطاع",
    personaPlantManager: "مدير المحطة",
    personaShiftOperator: "مشغل الوردية",
    personaReliability: "الاعتمادية",
    ask: "اسأل",
    askPlaceholder: "اسأل عن لوحة COA",
    hint: "إثبات المفهوم — السؤال نفسه يعيد الإجابة نفسها دائماً. دون استدعاء نموذج.",
    pushTitle: "تنبيه استباقي",
    loadBrief: "تحميل موجز الدور",
    briefEmpty: "اختر دوراً ثم حمّل التنبيه المجدول أو إنذار التنبيه.",
    hide: "إخفاء",
    assistant: "المساعد",
    hideAssistant: "إخفاء المساعد",
    showAssistant: "إظهار المساعد",
    hidePush: "إخفاء التنبيه الاستباقي",
    showPush: "إظهار التنبيه الاستباقي",
    refresh: "تحديث",
    briefOffline: "تعذر تحميل الموجز دون اتصال. إجابات لوحة COA لا تحتاج إلى الواجهة."
  }
};

function containsArabic(text) {
  return /[\u0600-\u06FF]/.test(text || "");
}

function answerLanguage(message) {
  if (containsArabic(message)) return "ar";
  if (langSelect && langSelect.value === "ar") return "ar";
  return "en";
}

function localize(lang, en, ar) {
  return lang === "ar" ? ar : en;
}

function applyChromeLanguage(lang) {
  const pack = chromeText[lang] || chromeText.en;
  document.querySelectorAll("[data-i18n]").forEach((el) => {
    if (el.id === "briefCard" && el.dataset.filled === "1") return;
    const value = pack[el.dataset.i18n];
    if (value != null) el.textContent = value;
  });
  document.querySelectorAll("[data-i18n-placeholder]").forEach((el) => {
    const value = pack[el.dataset.i18nPlaceholder];
    if (value != null) el.placeholder = value;
  });
  document.documentElement.lang = lang === "ar" ? "ar" : "en";
  const toggle = document.getElementById("chatToggle");
  if (toggle) {
    const collapsed = document.querySelector(".layout").classList.contains("chat-collapsed");
    toggle.title = collapsed ? pack.showAssistant : pack.hideAssistant;
  }
  const pushToggle = document.getElementById("pushToggle");
  const pushPanel = document.getElementById("pushPanel");
  if (pushToggle && pushPanel) {
    const pushCollapsed = pushPanel.classList.contains("is-collapsed");
    pushToggle.title = pushCollapsed ? pack.showPush : pack.hidePush;
  }
}

function addMessage(role, text, extras) {
  const div = document.createElement("div");
  div.className = `msg cb-msg ${role}`;
  if (containsArabic(text)) div.dir = "rtl";
  div.textContent = text;
  if (extras?.chartSvg) {
    const chart = document.createElement("div");
    chart.className = "cb-chart";
    chart.dir = "ltr";
    chart.innerHTML = extras.chartSvg;
    div.appendChild(chart);
  }
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
  const list = (langSelect.value === "ar" && promptsByPersonaAr[personaSelect.value])
    ? promptsByPersonaAr[personaSelect.value]
    : (promptsByPersona[personaSelect.value] || []);
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

function formatTs(value) {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? String(value ?? "") : d.toLocaleString();
}

function renderVisualization(payload) {
  if (!payload?.series?.length) {
    vizMeta.textContent = "";
    if (chart) {
      chart.destroy();
      chart = null;
    }
    return;
  }
  if (typeof Chart === "undefined") {
    vizMeta.textContent = "Chart library did not load.";
    return;
  }
  const ctx = document.getElementById("trendChart");
  const labels = payload.series[0].points.map(p => formatTs(p.timestampUtc));
  const datasets = payload.series.map((s, i) => ({
    label: `${s.name}${s.unit ? ` (${s.unit})` : ""}`,
    data: s.points.map(p => p.value),
    borderColor: ["#2ec4ff", "#3ecf8e", "#e6b84d", "#f07178"][i % 4],
    tension: 0.15,
    pointRadius: 0,
    borderWidth: 2
  }));
  if (chart) chart.destroy();
  chart = new Chart(ctx, {
    type: "line",
    data: { labels, datasets },
    options: {
      responsive: true,
      scales: {
        x: { ticks: { color: "#8fa3bc", maxRotation: 0 }, grid: { color: "#1c2e44" } },
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

// TODO: replace with real LLM integration post-POC.
// Fixed answers from the COA board (reqs/Sector-Level.png). No /api/chat call and no model.
const topPlantAnswer = "PP10 is the top plant on the COA board at 2,781 MW.\n\nNext are PP09 at 2,196 MW and PP12 at 1,560 MW. COA gross generation is 12,806 MW.";
const breakdownAnswer = "COA gross power is 12,806 MW across 11 plants.\n\nCombined cycle: 9,811 MW — PP10 2,781, PP09 2,196, PP12 1,560, PP14 1,523, PP13 1,511, QCPP 240.\nGas: 2,648 MW — PP08 1,175, PP07 1,008, HAIL 465.\nSteam: 348 MW — JUBA 266, LAYLA 82.";
const loadingAnswer = "COA loading factor is 71% on 18,116 MW total capacity.\n\nGross power is 12,806 MW and net power is 12,805 MW. In-service units: 179 of 232.";
const healthAnswer = "Healthy plants are green, data-unhealthy plants are yellow, and unavailability is red.\n\nData unhealthy on this board: HAIL 465 MW, PP08 1,175 MW, and PP07 1,008 MW. No plant is marked unavailable.";
const yesterdayAnswer = "Generation yesterday (Gen MWh Ydy) was 222,651 MWh.";
const fuelAnswer = "Yesterday's fuel consumption:\n• Heavy oil: 0 t\n• Crude oil: 4,600 t\n• Distillate oil: 570 t\n• Natural gas: 31,192 t";
const peakAnswer = "Daily peak is 14,553 MW at 6:05 PM.\nAnnual peak is 16,208 MW on 22/06.";
const defaultAnswer = "COA snapshot from this board:\n• Gross 12,806 MW · Net 12,805 MW · Capacity 18,116 MW\n• Loading factor 71% · In service 179 of 232\n• Daily peak 14,553 MW at 6:05 PM · Annual peak 16,208 MW on 22/06\n• Generation yesterday 222,651 MWh\n• Top plant PP10 at 2,781 MW\n\nTry: top plant, sector breakdown, unhealthy plants, or yesterday's fuel.";

const topPlantAnswerAr = "PP10 هي أعلى محطة على لوحة COA بقدرة 2,781 ميجاواط.\n\nتليها PP09 بقدرة 2,196 ميجاواط ثم PP12 بقدرة 1,560 ميجاواط. إجمالي توليد COA هو 12,806 ميجاواط.";
const breakdownAnswerAr = "القدرة الإجمالية لـ COA هي 12,806 ميجاواط عبر 11 محطة.\n\nالدورة المركبة: 9,811 ميجاواط — PP10 2,781، PP09 2,196، PP12 1,560، PP14 1,523، PP13 1,511، QCPP 240.\nالغاز: 2,648 ميجاواط — PP08 1,175، PP07 1,008، HAIL 465.\nالبخار: 348 ميجاواط — JUBA 266، LAYLA 82.";
const loadingAnswerAr = "معامل تحميل COA هو 71% من قدرة إجمالية 18,116 ميجاواط.\n\nالقدرة الإجمالية 12,806 ميجاواط والصافية 12,805 ميجاواط. الوحدات في الخدمة: 179 من 232.";
const healthAnswerAr = "المحطات السليمة باللون الأخضر، والمحطات ذات البيانات غير السليمة باللون الأصفر، وعدم التوفر باللون الأحمر.\n\nالبيانات غير السليمة على هذه اللوحة: HAIL 465 ميجاواط، PP08 1,175 ميجاواط، وPP07 1,008 ميجاواط. لا توجد محطة معلّمة كغير متاحة.";
const yesterdayAnswerAr = "توليد الأمس (Gen MWh Ydy) كان 222,651 ميجاواط ساعة.";
const fuelAnswerAr = "استهلاك الوقود أمس:\n• النفط الثقيل: 0 طن\n• النفط الخام: 4,600 طن\n• نواتج التقطير: 570 طن\n• الغاز الطبيعي: 31,192 طن";
const greetingEn = "Hi! I can answer questions about the COA sector board — top plant, sector breakdown, loading factor and in-service units, unhealthy plants, yesterday's generation and fuel, or a trend drawing.";
const greetingAr = "مرحباً! أستطيع الإجابة عن لوحة قطاع COA — أفضل محطة، توزيع القطاع، معامل التحميل والوحدات في الخدمة، المحطات غير السليمة، توليد ووقود الأمس، أو رسم اتجاه.";
const helpEn = "You can ask me things like:\n• What's the top-performing plant in COA?\n• Show me the sector breakdown\n• What's the loading factor and in-service count?\n• Which plants have unhealthy data?\n• What are yesterday's generation and fuel figures?\n• Draw a trend, chart, or graph";
const helpAr = "يمكنك أن تسألني مثلاً:\n• ما أفضل محطة أداءً في COA؟\n• أرني توزيع القطاع\n• ما معامل التحميل وعدد الوحدات في الخدمة؟\n• أي المحطات بياناتها غير سليمة؟\n• ما أرقام التوليد والوقود لأمس؟\n• ارسم اتجاهاً أو مخططاً أو رسماً بيانياً";
const helpFallbackEn = "Here's what I can help with:\n• What's the top-performing plant in COA?\n• Show me the sector breakdown\n• What's the loading factor and in-service count?\n• Which plants have unhealthy data?\n• What are yesterday's generation and fuel figures?\n• Draw a trend, chart, or graph";
const helpFallbackAr = "إليك ما يمكنني المساعدة فيه:\n• ما أفضل محطة أداءً في COA؟\n• أرني توزيع القطاع\n• ما معامل التحميل وعدد الوحدات في الخدمة؟\n• أي المحطات بياناتها غير سليمة؟\n• ما أرقام التوليد والوقود لأمس؟\n• ارسم اتجاهاً أو مخططاً أو رسماً بيانياً";
const defaultAnswerAr = "لقطة COA من هذه اللوحة:\n• إجمالي 12,806 ميجاواط · صافي 12,805 ميجاواط · القدرة 18,116 ميجاواط\n• معامل التحميل 71% · في الخدمة 179 من 232\n• الذروة اليومية 14,553 ميجاواط عند 6:05 م · الذروة السنوية 16,208 ميجاواط في 22/06\n• توليد الأمس 222,651 ميجاواط ساعة\n• أعلى محطة PP10 بقدرة 2,781 ميجاواط\n\nجرّب: أعلى محطة، توزيع القطاع، المحطات غير السليمة، أو وقود الأمس.";

const curatedCharts = [
  {
    title: { en: "Generation trend", ar: "اتجاه التوليد" },
    body: {
      en: "COA plant output on this board (MW): HAIL 465 · PP13 1,511 · PP12 1,560 · PP08 1,175 · PP10 2,781 · PP14 1,523 · QCPP 240 · LAYLA 82 · JUBA 266 · PP07 1,008 · PP09 2,196. Gross generation is 12,806 MW.",
      ar: "إنتاج محطات COA على هذه اللوحة (ميجاواط): HAIL 465 · PP13 1,511 · PP12 1,560 · PP08 1,175 · PP10 2,781 · PP14 1,523 · QCPP 240 · LAYLA 82 · JUBA 266 · PP07 1,008 · PP09 2,196. إجمالي التوليد 12,806 ميجاواط."
    },
    color: "#f6d365",
    points: [465, 1511, 1560, 1175, 2781, 1523, 240, 82, 266, 1008, 2196]
  },
  {
    title: { en: "Fuel mix breakdown", ar: "توزيع مزيج الوقود" },
    body: {
      en: "Yesterday's fuel (t): heavy oil 0 · crude oil 4,600 · distillate oil 570 · natural gas 31,192.",
      ar: "وقود الأمس (طن): نفط ثقيل 0 · نفط خام 4,600 · نواتج التقطير 570 · غاز طبيعي 31,192."
    },
    color: "#3aa0ff",
    points: [0, 4600, 570, 31192]
  },
  {
    title: { en: "Loading factor", ar: "معامل التحميل" },
    body: {
      en: "Loading factor is 71%. The line is the board's gross 12,806 MW, net 12,805 MW, and total capacity 18,116 MW.",
      ar: "معامل التحميل 71%. الخط يبيّن القدرة الإجمالية 12,806 ميجاواط، والصافية 12,805 ميجاواط، والقدرة الكلية 18,116 ميجاواط."
    },
    color: "#3ec4ff",
    points: [12806, 12805, 18116]
  },
  {
    title: { en: "Sector comparison", ar: "مقارنة القطاع" },
    body: {
      en: "COA generation by technology (MW): combined cycle 9,811 · gas 2,648 · steam 348. EOA, SOA, and WOA totals are not on this board.",
      ar: "توليد COA حسب التقنية (ميجاواط): دورة مركبة 9,811 · غاز 2,648 · بخار 348. مجاميع EOA وSOA وWOA غير ظاهرة على هذه اللوحة."
    },
    color: "#3ddc84",
    points: [9811, 2648, 348]
  },
  {
    title: { en: "Plant health snapshot", ar: "لقطة سلامة المحطات" },
    body: {
      en: "Data unhealthy (MW): HAIL 465 · PP08 1,175 · PP07 1,008. Unavailable: 0 MW. Every other COA plant on this board is healthy.",
      ar: "بيانات غير سليمة (ميجاواط): HAIL 465 · PP08 1,175 · PP07 1,008. غير متاح: 0 ميجاواط. بقية محطات COA على هذه اللوحة سليمة."
    },
    color: "#f0c14a",
    points: [465, 1175, 1008, 0]
  }
];

function sparklineSvg(points, color, label) {
  const h = 72;
  const w = 220;
  const min = Math.min(...points);
  const max = Math.max(...points);
  const rng = (max - min) || 1;
  const pts = points.map((v, i) => {
    const x = points.length === 1 ? w / 2 : (i / (points.length - 1)) * w;
    const y = h - 2 - ((v - min) / rng) * (h - 4);
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  }).join(" ");
  const safe = String(label).replace(/[&<>"]/g, "");
  return `<svg class="cb-spark" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none" role="img" aria-label="${safe}"><title>${safe}</title><polyline points="${pts}" fill="none" stroke="${color}" stroke-width="1.6" vector-effect="non-scaling-stroke"/></svg>`;
}

function curatedChartAnswer(lang) {
  const chart = curatedCharts[Math.floor(Math.random() * curatedCharts.length)];
  const title = lang === "ar" ? chart.title.ar : chart.title.en;
  const body = lang === "ar" ? chart.body.ar : chart.body.en;
  return { text: `${title}\n\n${body}`, svg: sparklineSvg(chart.points, chart.color, title) };
}

function matchSectorAnswer(message) {
  const q = message.trim().toLowerCase();
  const lang = answerLanguage(message);
  if ((/yesterday|ydy/.test(q) && /fuel|generat|mwh|oil|gas/.test(q)) || (/أمس|امس/.test(message) && /وقود|توليد|نفط|غاز/.test(message))) {
    return localize(lang, `${yesterdayAnswer}\n\n${fuelAnswer}`, `${yesterdayAnswerAr}\n\n${fuelAnswerAr}`);
  }
  if (/heavy oil|crude|distill|dist oil|natural gas|fuel/.test(q) || /وقود|نفط ثقيل|نفط خام|غاز طبيعي/.test(message)) return localize(lang, fuelAnswer, fuelAnswerAr);
  if (/peak/.test(q)) return peakAnswer;
  if (/mwh|yesterday|ydy/.test(q) || /أمس|امس/.test(message)) return localize(lang, yesterdayAnswer, yesterdayAnswerAr);
  if (/unhealthy|unavailable|outage|legend|health/.test(q) || /غير سليمة|غير صحية|عدم التوفر/.test(message)) return localize(lang, healthAnswer, healthAnswerAr);
  if (/loading|in-service|in service|capacity|gross|net power/.test(q) || /معامل التحميل|في الخدمة/.test(message)) return localize(lang, loadingAnswer, loadingAnswerAr);
  if (/breakdown|by tech|technolog|mix/.test(q) || /توزيع القطاع|حسب التقنية|مزيج التوليد|تفصيل القطاع/.test(message)) return localize(lang, breakdownAnswer, breakdownAnswerAr);
  if (/top[- ]performing sector|which sector|best sector|largest generation decline/.test(q)) {
    return "This screen is the Central Operating Area (COA), generating 12,806 MW gross and 12,805 MW net.\n\nEOA, SOA, and WOA totals are not on this board. The largest COA plant is PP10 at 2,781 MW.";
  }
  if (/top|best plant|highest|largest plant|performing/.test(q) || /أفضل محطة|افضل محطة|أعلى محطة|اعلى محطة|الأعلى أداء|الأكثر إنتاج/.test(message)) return localize(lang, topPlantAnswer, topPlantAnswerAr);
  if (/pp09|heat rate|shortfall|block/.test(q)) {
    return "PP09 is generating 2,196 MW and is marked healthy on the COA board. Heat-rate and block detail are not on this sector display.";
  }
  if (/gt01|flame|shift/.test(q)) {
    return "GT01 is not on the sector board. Its plant, PP09, is at 2,196 MW and marked healthy. Open the unit screen for flame intensity.";
  }
  if (/baseline|reliability|30 day|comparable unit/.test(q)) {
    return "30-day baseline trends are not on the COA sector board. Current plant output is listed on the map; PP10 is highest at 2,781 MW and PP09 is 2,196 MW.";
  }
  if (/محط|تحميل|قطاع/.test(q)) {
    return "معامل تحميل COA هو 71% من قدرة 18,116 MW. القدرة الإجمالية 12,806 MW والصافية 12,805 MW. الوحدات في الخدمة: 179 من 232.\nالمحطات ذات البيانات غير السليمة: HAIL (465 MW)، PP08 (1,175 MW)، PP07 (1,008 MW). أهداف التحميل لكل محطة غير ظاهرة على هذه الشاشة.";
  }
  if (/plant|hail|qcpp|layla|juba|pp1|pp0|map/.test(q)) {
    return "COA plants on this board:\nHAIL 465 MW · PP13 1,511 · PP12 1,560 · PP08 1,175 · PP10 2,781 · PP14 1,523\nQCPP 240 · LAYLA 82 · JUBA 266 · PP07 1,008 · PP09 2,196 MW.";
  }
  if (/^(hi|hello|hey|hola|salam|marhaba)\b/i.test(q) || /^(مرحبا|أهلا|اهلا|السلام|سلام)/.test(message.trim())) {
    const greetAr = containsArabic(message) || /^(salam|marhaba)\b/i.test(q);
    return greetAr ? greetingAr : greetingEn;
  }
  if (/help|what can you|capabilit/i.test(q) || /مساعدة|ماذا تستطيع|ما الذي يمكنك|القدرات/.test(message)) {
    return localize(lang, helpEn, helpAr);
  }
  if (/trend|chart|graph|plot|draw|drawing|visualiz/i.test(q) || /اتجاه|مخطط|رسم/.test(message)) {
    return curatedChartAnswer(lang);
  }
  if (/snapshot|overview|summary/.test(q)) return localize(lang, defaultAnswer, defaultAnswerAr);
  return localize(lang, helpFallbackEn, helpFallbackAr);
}

const coaSeries = [
  { name: "COA", mw: 12806, color: "#f6d365", top: true },
  { name: "PP07", mw: 1008, color: "#4fc3f7" },
  { name: "PP08", mw: 1175, color: "#3ddc84" },
  { name: "PP09", mw: 2196, color: "#f0c14a" },
  { name: "PP10", mw: 2781, color: "#ff8a65" },
  { name: "PP12", mw: 1560, color: "#b388ff" },
  { name: "PP13", mw: 1511, color: "#69f0ae" },
  { name: "PP14", mw: 1523, color: "#80d8ff" }
];

function renderTrendLegend() {
  const list = document.getElementById("trendLegend");
  if (!list || list.childElementCount) return;
  coaSeries.forEach(s => {
    const li = document.createElement("li");
    li.innerHTML = `<i style="background:${s.color}"></i><span><b>${s.name}</b> ${s.mw.toLocaleString()} MW</span>`;
    list.appendChild(li);
  });
}

function formatTrendTick(d) {
  const h = d.getHours();
  const ampm = h >= 12 ? "PM" : "AM";
  const hr = h % 12 || 12;
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getMonth() + 1}/${d.getDate()}/${d.getFullYear()} ${hr}:${p(d.getMinutes())}:${p(d.getSeconds())} ${ampm}`;
}

// Last point is the value printed on the board. The path between endpoints is illustrative.
function sectorTrend(endValue, amplitude, dip) {
  const n = 48;
  const start = new Date(2026, 8, 15, 14, 42, 44).getTime();
  const step = (24 * 60 * 60 * 1000) / (n - 1);
  const values = [];
  const labels = [];
  for (let i = 0; i < n; i++) {
    let v = endValue + Math.sin(i / 2.7) * amplitude + Math.cos(i / 5.1) * amplitude * 0.35;
    if (dip && i >= n - 8 && i < n - 1) v -= amplitude * 0.35 * (i - (n - 8));
    if (i === n - 1) v = endValue;
    values.push(Math.round(Math.max(0, v)));
    const d = new Date(start + i * step);
    labels.push(i === 0 || i === n - 1 ? formatTrendTick(d) : "");
  }
  return { values, labels };
}

function darkLineChart(canvas, labels, datasets, yMax) {
  return new Chart(canvas, {
    type: "line",
    data: { labels, datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: {
        legend: { display: false },
        tooltip: { backgroundColor: "#0b2744" }
      },
      scales: {
        x: {
          ticks: { color: "#8eb4d4", font: { size: 8 }, maxRotation: 0, autoSkip: false },
          grid: { color: "rgba(110, 160, 200, 0.18)" },
          border: { color: "rgba(110, 160, 200, 0.35)" }
        },
        y: {
          min: 0,
          max: yMax,
          ticks: { color: "#8eb4d4", font: { size: 9 }, maxTicksLimit: 9 },
          grid: { color: "rgba(110, 160, 200, 0.22)" },
          border: { color: "rgba(110, 160, 200, 0.35)" }
        }
      }
    }
  });
}

function ensureSectorCharts() {
  renderTrendLegend();
  if (sectorCharts.length) {
    sectorCharts.forEach(c => c.resize());
    return;
  }
  if (typeof Chart === "undefined") return;
  const top = document.getElementById("coaTrendTop");
  const bottom = document.getElementById("coaTrendBottom");
  if (!top || !bottom || top.clientHeight < 20) {
    if (sectorChartTries++ < 12) requestAnimationFrame(ensureSectorCharts);
    return;
  }
  const coa = sectorTrend(12806, 1400, true);
  const plantSets = coaSeries.filter(s => !s.top).map(s => {
    const series = sectorTrend(s.mw, Math.max(40, s.mw * 0.08), false);
    return {
      label: s.name,
      data: series.values,
      borderColor: s.color,
      borderWidth: 1.5,
      pointRadius: 0,
      tension: 0.25
    };
  });
  sectorCharts = [
    darkLineChart(top, coa.labels, [{
      label: "COA",
      data: coa.values,
      borderColor: "#f6d365",
      borderWidth: 2,
      pointRadius: 0,
      tension: 0.25
    }], 16000),
    darkLineChart(bottom, coa.labels, plantSets, 4000)
  ];
}

async function loadScreen() {
  const level = currentLevel;
  const showSector = level === "sector";
  document.getElementById("sectorDash").hidden = !showSector;
  document.getElementById("genericScreen").hidden = showSector;
  if (showSector) {
    sectorChartTries = 0;
    ensureSectorCharts();
    return;
  }
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
  try {
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
    briefCard.dataset.filled = "1";
  } catch {
    briefCard.dataset.filled = "1";
    briefCard.textContent = (chromeText[langSelect.value] || chromeText.en).briefOffline;
  }
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
langSelect.addEventListener("change", () => {
  applyChromeLanguage(langSelect.value === "ar" ? "ar" : "en");
  renderPrompts();
});
document.getElementById("refreshScreenBtn").addEventListener("click", loadScreen);
document.getElementById("loadBriefBtn").addEventListener("click", loadBrief);

chatForm.addEventListener("submit", (e) => {
  e.preventDefault();
  const message = chatInput.value.trim();
  if (!message) return;
  if (containsArabic(message) && langSelect.value !== "ar") {
    langSelect.value = "ar";
    applyChromeLanguage("ar");
    renderPrompts();
  }
  addMessage("user", message);
  chatInput.value = "";
  // TODO: replace with real LLM integration post-POC.
  const answer = matchSectorAnswer(message);
  const meta = {
    persona: personaSelect.value,
    model: "POC mock",
    action: "Review the COA sector board",
    jump: "PI Vision://Sector/COA"
  };
  if (answer && typeof answer === "object") addMessage("bot", answer.text, { ...meta, chartSvg: answer.svg });
  else addMessage("bot", answer, meta);
});

personaSelect.value = levelToPersona[currentLevel] || "Executive";
renderPrompts();
renderTrendLegend();
addMessage("bot", "COA sector board is on the left. Pick a prompt or ask in your own words — answers are fixed for this POC and do not call a model.", {
  model: "POC mock"
});
loadModels();
refreshHealth();
loadScreen();
loadBrief();
setInterval(refreshHealth, 30000);
window.addEventListener("resize", () => sectorCharts.forEach(c => c.resize()));

document.getElementById("chatToggle").addEventListener("click", () => {
  const layout = document.querySelector(".layout");
  const collapsed = layout.classList.toggle("chat-collapsed");
  const toggle = document.getElementById("chatToggle");
  toggle.setAttribute("aria-expanded", String(!collapsed));
  const pack = chromeText[langSelect.value === "ar" ? "ar" : "en"];
  toggle.title = collapsed ? pack.showAssistant : pack.hideAssistant;
  const icon = toggle.querySelector(".chat-toggle-icon");
  if (icon) icon.textContent = collapsed ? "‹" : "›";
  requestAnimationFrame(() => sectorCharts.forEach(c => c.resize()));
});

document.getElementById("pushToggle").addEventListener("click", () => {
  const panel = document.getElementById("pushPanel");
  const body = document.getElementById("pushBody");
  const toggle = document.getElementById("pushToggle");
  const collapsed = panel.classList.toggle("is-collapsed");
  body.hidden = collapsed;
  toggle.setAttribute("aria-expanded", String(!collapsed));
  const pack = chromeText[langSelect.value === "ar" ? "ar" : "en"];
  toggle.title = collapsed ? pack.showPush : pack.hidePush;
  const icon = toggle.querySelector(".push-toggle-icon");
  if (icon) icon.textContent = collapsed ? "›" : "⌄";
});
