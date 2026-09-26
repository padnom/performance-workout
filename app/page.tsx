"use client";
import { useEffect, useMemo, useState } from "react";
type SetRow = { weight: string; reps: string; rir: string };
type Gym = "Toutes" | "One Air" | "Basic-Fit";
type Exercise = {
  id: string;
  name: string;
  alt?: string;
  target: string;
  rest: string;
  sets: number;
  link?: string;
  group: string;
  tags?: string[];
  gym?: "Toutes" | "One Air" | "Basic-Fit";
  gyms?: Gym[];
};
type Session = {
  id: string;
  name: string;
  kicker: string;
  color: string;
  exercises: Exercise[];
};
type Log = {
  id: string;
  date: string;
  sessionId: string;
  duration: number;
  entries: Record<string, SetRow[]>;
};
type Equipment = { name: string; group: string; kind: string };
type ExerciseEdit = {
  name: string;
  group: string;
  sets: number;
  target: string;
  restSeconds: number;
  link: string;
  tags: string;
  gyms: Gym[];
};
type Variant = {
  id: string;
  name: string;
  description: string;
  exerciseIds?: string[];
  mode: "balanced" | "strength" | "hypertrophy" | "short";
};
const product = (slug: string) =>
  `https://shop.lifefitness.com/products/${slug}`;
const ex = (
  id: string,
  name: string,
  target: string,
  rest: string,
  sets: number,
  group: string,
  alt?: string,
  link?: string,
): Exercise => ({ id, name, target, rest, sets, group, alt, link, gym: /Hammer|Life Fitness/.test(name) ? "One Air" : "Toutes" });
const sessions: Session[] = [
  {
    id: "push",
    name: "Push",
    kicker: "Pectoraux · Épaules · Triceps",
    color: "#ff5c57",
    exercises: [
      ex(
        "bench",
        "Développé couché barre",
        "4 × 6–8",
        "2–3 min",
        4,
        "Pectoraux",
        "Hammer Horizontal Bench Press",
        product(
          "hammer-strength-plate-loaded-iso-lateral-horizontal-bench-press",
        ),
      ),
      ex(
        "incline",
        "Hammer Iso-Lateral Incline Press",
        "4 × 8–10",
        "2 min",
        4,
        "Pectoraux",
        "Développé incliné haltères",
        product("hammer-strength-plate-loaded-iso-lateral-incline-press"),
      ),
      ex(
        "fly",
        "Life Fitness Pectoral Fly",
        "4 × 10–15",
        "1 min 30",
        4,
        "Pectoraux",
        "Écartés à la poulie",
      ),
      ex(
        "shoulder",
        "Hammer Iso-Lateral Shoulder Press",
        "4 × 8–10",
        "2 min",
        4,
        "Épaules",
        "Développé haltères assis",
        product("hammer-strength-plate-loaded-iso-lateral-shoulder-press"),
      ),
      ex(
        "lateral",
        "Hammer Plate Loaded Lateral Raise",
        "4 × 12–20",
        "1–1 min 30",
        4,
        "Épaules",
        "Élévations haltères / poulie",
        product("hammer-strength-plate-loaded-lateral-raise"),
      ),
      ex(
        "rope",
        "Extension corde poulie",
        "4 × 10–15",
        "1–1 min 30",
        4,
        "Triceps",
        "Life Fitness Triceps Extension",
      ),
      ex("plank", "Planche", "4 × 30–45 s", "1 min", 4, "Abdominaux"),
    ],
  },
  {
    id: "pull",
    name: "Pull",
    kicker: "Dos · Biceps · Arrière d’épaule",
    color: "#5279ff",
    exercises: [
      ex(
        "pulldown",
        "Hammer Front Lat Pulldown",
        "4 × 6–10",
        "2 min",
        4,
        "Dos",
        "Life Fitness Pulldown / tractions",
        product("hammer-strength-plate-loaded-iso-lateral-wide-pulldown"),
      ),
      ex(
        "lowrow",
        "Hammer Iso-Lateral Low Row",
        "4 × 8–10",
        "2 min",
        4,
        "Dos",
        "Rowing haltère unilatéral",
        product("hammer-strength-plate-loaded-iso-lateral-low-row"),
      ),
      ex(
        "row",
        "Hammer Iso-Lateral Row",
        "4 × 10–12",
        "1 min 30–2 min",
        4,
        "Dos",
        "Life Fitness Row",
        product("hammer-strength-plate-loaded-iso-lateral-row"),
      ),
      ex(
        "rear",
        "Life Fitness Rear Deltoid",
        "4 × 12–20",
        "1 min 30",
        4,
        "Épaules",
        "Reverse Fly poulie",
      ),
      ex(
        "curl",
        "Curl incliné haltères",
        "4 × 8–12",
        "1 min 30",
        4,
        "Biceps",
        "Life Fitness Biceps Curl",
      ),
      ex(
        "hammer-curl",
        "Curl marteau haltères",
        "4 × 10–15",
        "1 min 30",
        4,
        "Biceps",
        "Curl marteau poulie",
      ),
      ex(
        "abs",
        "Life Fitness Abdominal",
        "4 × 10–15",
        "1 min",
        4,
        "Abdominaux",
        "Crunch poulie",
      ),
    ],
  },
  {
    id: "legs",
    name: "Jambes",
    kicker: "Quadriceps · Ischios · Fessiers",
    color: "#31b57a",
    exercises: [
      ex(
        "squat",
        "Hammer Pendulum-X Squat",
        "4 × 6–10",
        "2–3 min",
        4,
        "Jambes",
        "Life Fitness Leg Press",
        product("hammer-strength-plate-loaded-pendulum-x-squat"),
      ),
      ex(
        "rdl",
        "Soulevé de terre roumain barre",
        "4 × 8–10",
        "2–3 min",
        4,
        "Jambes",
        "Soulevé de terre roumain haltères",
      ),
      ex(
        "legcurl",
        "Life Fitness Leg Curl",
        "4 × 10–15",
        "1 min 30–2 min",
        4,
        "Jambes",
        "Leg Curl plate-loaded",
      ),
      ex(
        "glute",
        "Hammer Glute Drive",
        "4 × 8–12",
        "2 min",
        4,
        "Jambes",
        "Hammer Belt Squat",
        product("hammer-strength-plate-loaded-glute-drive"),
      ),
      ex(
        "legext",
        "Hammer Iso-Lateral Leg Extension",
        "4 × 12–15",
        "1 min 30",
        4,
        "Jambes",
        "Leg Extension plate-loaded",
        product("hammer-strength-plate-loaded-iso-lateral-leg-extension"),
      ),
      ex(
        "calf",
        "Hammer Seated Calf Raise",
        "4 × 10–15",
        "1 min 30",
        4,
        "Jambes",
        "Mollets debout",
        product("hammer-strength-plate-loaded-seated-calf-raise"),
      ),
      ex(
        "abduction",
        "Life Fitness Hip Abduction",
        "4 × 12–20",
        "1 min",
        4,
        "Jambes",
      ),
    ],
  },
  {
    id: "upper",
    name: "Upper",
    kicker: "Haut du corps complet",
    color: "#ff953f",
    exercises: [
      ex(
        "incline-db",
        "Développé incliné haltères",
        "4 × 8–10",
        "2 min",
        4,
        "Pectoraux",
        "Hammer Incline Press",
        product("hammer-strength-plate-loaded-iso-lateral-incline-press"),
      ),
      ex(
        "pulldown-u",
        "Hammer Front Lat Pulldown",
        "4 × 8–12",
        "2 min",
        4,
        "Dos",
        "Life Fitness Pulldown / tractions",
        product("hammer-strength-plate-loaded-iso-lateral-wide-pulldown"),
      ),
      ex(
        "dip",
        "Hammer Seated Dip",
        "4 × 8–12",
        "2 min",
        4,
        "Triceps",
        "Dips poids du corps",
        product("hammer-strength-plate-loaded-seated-dip"),
      ),
      ex(
        "row-u",
        "Hammer Iso-Lateral Row",
        "4 × 8–12",
        "2 min",
        4,
        "Dos",
        "Rowing haltère unilatéral",
        product("hammer-strength-plate-loaded-iso-lateral-row"),
      ),
      ex(
        "lateral-u",
        "Élévations latérales poulie",
        "4 × 12–20",
        "1–1 min 30",
        4,
        "Épaules",
        "Hammer Lateral Raise",
        product("hammer-strength-plate-loaded-lateral-raise"),
      ),
      ex(
        "biceps-u",
        "Life Fitness Biceps Curl",
        "4 × 10–15",
        "1 min 30",
        4,
        "Biceps",
        "Curl haltères",
      ),
      ex(
        "triceps-u",
        "Extension au-dessus de la tête poulie",
        "4 × 10–15",
        "1 min 30",
        4,
        "Triceps",
        "Life Fitness Triceps Extension",
      ),
      ex(
        "pallof",
        "Pallof Press",
        "4 × 12–15 / côté",
        "1 min",
        4,
        "Abdominaux",
      ),
    ],
  },
  {
    id: "arms",
    name: "Bras + épaules",
    kicker: "Séance optionnelle",
    color: "#a66cff",
    exercises: [
      ex(
        "lateral-a",
        "Hammer Lateral Raise",
        "4 × 12–20",
        "1–1 min 30",
        4,
        "Épaules",
        "Poulie / haltères",
        product("hammer-strength-plate-loaded-lateral-raise"),
      ),
      ex(
        "rear-a",
        "Life Fitness Rear Deltoid",
        "4 × 12–20",
        "1 min 30",
        4,
        "Épaules",
        "Reverse Fly poulie",
      ),
      ex(
        "curl-a",
        "Curl incliné haltères",
        "4 × 8–12",
        "1 min 30",
        4,
        "Biceps",
        "Life Fitness Biceps Curl",
      ),
      ex(
        "hammer-a",
        "Curl marteau haltères",
        "4 × 10–15",
        "1 min 30",
        4,
        "Biceps",
        "Poulie",
      ),
      ex(
        "rope-a",
        "Extension corde poulie",
        "4 × 10–15",
        "1 min 30",
        4,
        "Triceps",
        "Life Fitness Triceps Extension",
      ),
      ex(
        "overhead-a",
        "Extension au-dessus tête poulie",
        "4 × 10–15",
        "1 min 30",
        4,
        "Triceps",
        "Haltère",
      ),
      ex(
        "abs-a",
        "Life Fitness Abdominal",
        "4 × 10–15",
        "1 min",
        4,
        "Abdominaux",
        "Crunch poulie",
      ),
    ],
  },
];
sessions.push(
  {
    id: "full",
    name: "Full Body",
    kicker: "Corps entier",
    color: "#f59e42",
    exercises: [],
  },
  {
    id: "chest-biceps",
    name: "Pectoraux + biceps",
    kicker: "Pectoraux · Biceps",
    color: "#ff6b76",
    exercises: [],
  },
  {
    id: "back-triceps",
    name: "Dos + triceps",
    kicker: "Dos · Triceps",
    color: "#6386ff",
    exercises: [],
  },
  {
    id: "chest-back",
    name: "Pectoraux + dos",
    kicker: "Pectoraux · Dos",
    color: "#a06cff",
    exercises: [],
  },
  {
    id: "quads-calves",
    name: "Quadriceps + mollets",
    kicker: "Quadriceps · Mollets",
    color: "#36c98b",
    exercises: [],
  },
  {
    id: "hams-glutes",
    name: "Ischios + fessiers",
    kicker: "Ischios · Fessiers",
    color: "#20b8a9",
    exercises: [],
  },
);
const extraExercises: Exercise[] = [
  ex("lf-abdominal", "Life Fitness Abdominal", "4 × 10–15", "1 min 30", 4, "Abdominaux", "Crunch à la poulie"),
  ex("lf-torso-rotation", "Life Fitness Torso Rotation", "4 × 10–15 / côté", "1 min 30", 4, "Obliques", "Rotation à la poulie"),
  ex("lf-chest-press", "Life Fitness Chest Press", "4 × 8–12", "2 min", 4, "Pectoraux", "Développé couché haltères"),
  ex("lf-incline-press", "Life Fitness Incline Press", "4 × 8–12", "2 min", 4, "Pectoraux", "Développé incliné haltères"),
  ex("lf-shoulder-press", "Life Fitness Shoulder Press", "4 × 8–12", "2 min", 4, "Épaules", "Développé épaules haltères"),
  ex("lf-lateral-raise", "Life Fitness Lateral Raise", "4 × 10–15", "1 min 30", 4, "Épaules", "Élévations latérales haltères"),
  ex("lf-pulldown", "Life Fitness Pulldown", "4 × 8–12", "2 min", 4, "Dos", "Tirage vertical à la poulie"),
  ex("lf-row", "Life Fitness Seated Row", "4 × 8–12", "2 min", 4, "Dos", "Rowing poulie basse"),
  ex("lf-biceps", "Life Fitness Biceps Curl", "4 × 8–12", "1 min 30", 4, "Biceps", "Curl pupitre"),
  ex("lf-triceps", "Life Fitness Triceps Extension", "4 × 8–12", "1 min 30", 4, "Triceps", "Extension corde à la poulie"),
  ex("lf-leg-press", "Life Fitness Leg Press", "4 × 8–12", "2 min", 4, "Quadriceps", "Presse à cuisses"),
  ex("lf-leg-extension", "Life Fitness Leg Extension", "4 × 8–12", "1 min 30", 4, "Quadriceps", "Leg extension guidé"),
  ex("lf-seated-leg-curl", "Life Fitness Seated Leg Curl", "4 × 8–12", "1 min 30", 4, "Ischio-jambiers", "Leg curl allongé"),
  ex("lf-lying-leg-curl", "Life Fitness Lying Leg Curl", "4 × 8–12", "1 min 30", 4, "Ischio-jambiers", "Leg curl assis"),
  ex("lf-glute", "Life Fitness Glute", "4 × 8–12 / côté", "1 min 30", 4, "Fessiers", "Extension de hanche à la poulie"),
  ex("lf-adduction", "Life Fitness Hip Adduction", "4 × 10–15", "1 min 30", 4, "Adducteurs", "Adduction à la poulie"),
  ex("lf-abduction", "Life Fitness Hip Abduction", "4 × 10–15", "1 min 30", 4, "Fessiers", "Abduction à la poulie"),
  ex("lf-calf", "Life Fitness Calf Extension", "4 × 10–15", "1 min 30", 4, "Mollets", "Mollets debout"),
  ex("lf-back-extension", "Life Fitness Back Extension", "4 × 10–15", "1 min 30", 4, "Lombaires", "Extension lombaire au banc"),
  ex(
    "bench-db",
    "Développé couché haltères",
    "4 × 8–12",
    "2 min",
    4,
    "Pectoraux",
    "Chest Press",
  ),
  ex(
    "incline-bar",
    "Développé incliné barre",
    "4 × 6–10",
    "2 min",
    4,
    "Pectoraux",
    "Incline Press",
  ),
  ex(
    "cable-fly",
    "Écarté à la poulie",
    "4 × 10–15",
    "1 min 30",
    4,
    "Pectoraux",
    "Pectoral Fly",
  ),
  ex(
    "pushup",
    "Pompes",
    "4 × 10–20",
    "1 min 30",
    4,
    "Pectoraux",
    "Chest Press",
  ),
  ex(
    "shoulder-db",
    "Développé épaules haltères",
    "4 × 8–12",
    "2 min",
    4,
    "Épaules",
    "Shoulder Press",
  ),
  ex(
    "lateral-db",
    "Élévations latérales haltères",
    "4 × 12–20",
    "1 min",
    4,
    "Épaules",
    "Lateral Raise",
  ),
  ex("pullup", "Tractions", "4 × 6–10", "2 min", 4, "Dos", "Tirage vertical"),
  ex(
    "cable-row",
    "Rowing poulie basse",
    "4 × 8–12",
    "2 min",
    4,
    "Dos",
    "Low Row",
  ),
  ex(
    "row-db",
    "Rowing haltère unilatéral",
    "4 × 8–12",
    "2 min",
    4,
    "Dos",
    "Iso-Lateral Row",
  ),
  ex(
    "pullover",
    "Pull-over à la poulie",
    "4 × 10–15",
    "1 min 30",
    4,
    "Dos",
    "Pulldown",
  ),
  ex(
    "facepull",
    "Face pull",
    "4 × 12–20",
    "1 min 30",
    4,
    "Épaules",
    "Rear Deltoid",
  ),
  ex(
    "curl-cable",
    "Curl à la poulie",
    "4 × 10–15",
    "1 min 30",
    4,
    "Biceps",
    "Biceps Curl",
  ),
  ex(
    "legpress",
    "Presse à cuisses",
    "4 × 8–12",
    "2–3 min",
    4,
    "Jambes",
    "Pendulum Squat",
  ),
  ex(
    "goblet",
    "Goblet squat",
    "4 × 10–15",
    "2 min",
    4,
    "Jambes",
    "Squat guidé",
  ),
  ex(
    "split-squat",
    "Fente bulgare",
    "4 × 8–12 / côté",
    "2 min",
    4,
    "Jambes",
    "Presse unilatérale",
  ),
  ex(
    "hipthrust",
    "Hip thrust barre",
    "4 × 8–12",
    "2 min",
    4,
    "Jambes",
    "Glute Drive",
  ),
  ex(
    "backext",
    "Extension lombaire",
    "4 × 10–15",
    "1 min 30",
    4,
    "Jambes",
    "Back Extension",
  ),
  ex(
    "close-bench",
    "Développé couché prise serrée",
    "4 × 6–10",
    "2 min",
    4,
    "Triceps",
    "Seated Dip",
  ),
  ex(
    "preacher",
    "Curl pupitre",
    "4 × 8–12",
    "1 min 30",
    4,
    "Biceps",
    "Biceps Curl",
  ),
  ex(
    "pushdown-bar",
    "Extension triceps barre",
    "4 × 10–15",
    "1 min 30",
    4,
    "Triceps",
    "Triceps Extension",
  ),
];
const icons: Record<string, string> = {
  home: "⌂",
  program: "▦",
  stats: "↗",
  history: "◷",
  settings: "⚙",
  exercises: "≡",
};
const normalizeExerciseName = (value: string) =>
  value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("fr")
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .replace(/^extension au dessus tete poulie$/, "extension triceps au dessus tete poulie");
const predefinedTagGroups = [
  { label: "Muscle", tags: ["Pectoraux", "Dos", "Grand dorsal", "Trapèzes", "Épaules", "Biceps", "Triceps", "Quadriceps", "Ischio-jambiers", "Fessiers", "Mollets", "Abdominaux"] },
  { label: "Type", tags: ["Polyarticulaire", "Isolation", "Unilatéral"] },
  { label: "Équipement", tags: ["Machine", "Poulie", "Haltères", "Barre", "Poids du corps"] },
  { label: "Mouvement", tags: ["Poussée", "Tirage vertical", "Tirage horizontal", "Flexion", "Extension", "Élévation"] },
] as const;
const inferredTags = (exercise: Exercise) => {
  const value = normalizeExerciseName(`${exercise.name} ${exercise.group}`);
  const tags = new Set<string>([exercise.group]);
  if (/hammer|life fitness|machine|guidee/.test(value)) tags.add("Machine");
  if (/poulie|pulldown|cable/.test(value)) tags.add("Poulie");
  if (/haltere/.test(value)) tags.add("Haltères");
  if (/barre/.test(value)) tags.add("Barre");
  if (/pompe|traction|planche/.test(value)) tags.add("Poids du corps");
  if (/iso lateral|unilateral|un cote|cote/.test(value)) tags.add("Unilatéral");
  if (/curl|extension|fly|ecarte|lateral|rear deltoid|oiseau|abduction|adduction|mollet|calf/.test(value)) tags.add("Isolation");
  else tags.add("Polyarticulaire");
  if (/developpe|press|pompe|dip/.test(value)) tags.add("Poussée");
  if (/pulldown|traction|tirage vertical/.test(value)) tags.add("Tirage vertical");
  if (/row|rowing|tirage horizontal/.test(value)) tags.add("Tirage horizontal");
  if (/elevation|lateral raise/.test(value)) tags.add("Élévation");
  if (/extension/.test(value)) tags.add("Extension");
  if (/curl|flexion/.test(value)) tags.add("Flexion");
  return [...tags];
};
const allExercises = [
  ...sessions.flatMap((s) => s.exercises),
  ...extraExercises,
];
const exerciseCatalog = Array.from(
  allExercises.reduce((catalog, exercise) => {
    const key = normalizeExerciseName(exercise.name);
    const existing = catalog.get(key);
    if (!existing) {
      catalog.set(key, { ...exercise, tags: [...new Set([...(exercise.tags || []), ...inferredTags(exercise)])], gyms: exercise.gyms || [exercise.gym || (/Hammer|Life Fitness/.test(exercise.name) ? "One Air" : "Toutes")] });
    } else {
      catalog.set(key, { ...existing, tags: [...new Set([...(existing.tags || []), ...(exercise.tags || []), ...inferredTags(exercise)])], gyms: [...new Set([...gymsForCatalog(existing), ...gymsForCatalog(exercise)])] });
    }
    return catalog;
  }, new Map<string, Exercise>()),
).map(([, exercise]) => exercise);
function gymsForCatalog(exercise: Exercise): Gym[] {
  return exercise.gyms || [exercise.gym || (/Hammer|Life Fitness/.test(exercise.name) ? "One Air" : "Toutes")];
}
const equipment: Equipment[] = [
  {
    name: "Hammer Iso-Lateral Incline Press",
    group: "Pectoraux",
    kind: "Plaques",
  },
  {
    name: "Life Fitness Pectoral Fly",
    group: "Pectoraux",
    kind: "Charge guidée",
  },
  {
    name: "Hammer Iso-Lateral Shoulder Press",
    group: "Épaules",
    kind: "Plaques",
  },
  {
    name: "Hammer Plate Loaded Lateral Raise",
    group: "Épaules",
    kind: "Plaques",
  },
  { name: "Hammer Front Lat Pulldown", group: "Dos", kind: "Plaques" },
  { name: "Hammer Iso-Lateral Low Row", group: "Dos", kind: "Plaques" },
  { name: "Hammer Iso-Lateral Row", group: "Dos", kind: "Plaques" },
  {
    name: "Life Fitness Rear Deltoid",
    group: "Épaules",
    kind: "Charge guidée",
  },
  {
    name: "Life Fitness Abdominal",
    group: "Abdominaux",
    kind: "Charge guidée",
  },
  { name: "Hammer Pendulum-X Squat", group: "Jambes", kind: "Plaques" },
  {
    name: "Life Fitness Leg Curl",
    group: "Ischio-jambiers",
    kind: "Charge guidée",
  },
  { name: "Hammer Glute Drive", group: "Fessiers", kind: "Plaques" },
  {
    name: "Hammer Iso-Lateral Leg Extension",
    group: "Quadriceps",
    kind: "Plaques",
  },
  { name: "Hammer Seated Calf Raise", group: "Mollets", kind: "Plaques" },
  {
    name: "Life Fitness Hip Abduction",
    group: "Fessiers",
    kind: "Charge guidée",
  },
  { name: "Hammer Seated Dip", group: "Triceps", kind: "Plaques" },
  { name: "Life Fitness Biceps Curl", group: "Biceps", kind: "Charge guidée" },
  {
    name: "Chaise romaine avec BOSU",
    group: "Abdominaux",
    kind: "Poids du corps",
  },
  {
    name: "Banc à lombaires réglable",
    group: "Lombaires",
    kind: "Poids du corps",
  },
  { name: "Machine Back Extension", group: "Lombaires", kind: "Charge guidée" },
  { name: "Machine Torso Rotation", group: "Obliques", kind: "Charge guidée" },
];
const variants: Record<string, Variant[]> = {
  push: [
    {
      id: "push-balanced",
      name: "Équilibré",
      description: "Pectoraux, épaules et triceps",
      mode: "balanced",
    },
    {
      id: "push-strength",
      name: "Force",
      description: "Mouvements principaux lourds",
      exerciseIds: ["bench", "incline", "shoulder", "rope", "plank"],
      mode: "strength",
    },
    {
      id: "push-hypertrophy",
      name: "Hypertrophie",
      description: "Volume et contrôle musculaire",
      mode: "hypertrophy",
    },
    {
      id: "push-chest",
      name: "Muscle A · Pectoraux",
      description: "Accent sur les trois angles de poussée",
      exerciseIds: ["bench", "incline", "fly", "shoulder", "rope", "plank"],
      mode: "hypertrophy",
    },
    {
      id: "push-strength-b",
      name: "Force B · Incliné",
      description: "Développé incliné en mouvement principal",
      exerciseIds: ["incline", "bench", "shoulder", "rope", "plank"],
      mode: "strength",
    },
    {
      id: "push-muscle-b",
      name: "Muscle B · Épaules",
      description: "Deltoïdes prioritaires, poussée conservée",
      exerciseIds: ["shoulder", "lateral", "incline", "fly", "rope", "plank"],
      mode: "hypertrophy",
    },
    {
      id: "push-mixed-b",
      name: "Mixte B · Machine",
      description: "Séance complète axée machines",
      exerciseIds: ["incline", "fly", "shoulder", "lateral", "rope", "plank"],
      mode: "balanced",
    },
    {
      id: "push-short",
      name: "Rapide · Essentiels",
      description: "Cinq exercices pour gagner du temps",
      exerciseIds: ["bench", "incline", "shoulder", "rope", "plank"],
      mode: "short",
    },
  ],
  pull: [
    {
      id: "pull-balanced",
      name: "Équilibré",
      description: "Largeur, épaisseur et biceps",
      mode: "balanced",
    },
    {
      id: "pull-strength",
      name: "Force",
      description: "Tirages lourds et repos longs",
      exerciseIds: ["pulldown", "lowrow", "row", "curl", "abs"],
      mode: "strength",
    },
    {
      id: "pull-width",
      name: "Dos en largeur",
      description: "Priorité aux tirages verticaux",
      exerciseIds: ["pulldown", "lowrow", "rear", "curl", "hammer-curl", "abs"],
      mode: "hypertrophy",
    },
    {
      id: "pull-thickness",
      name: "Muscle B · Épaisseur",
      description: "Priorité aux rowings",
      exerciseIds: ["lowrow", "row", "pulldown", "rear", "curl", "abs"],
      mode: "hypertrophy",
    },
    {
      id: "pull-strength-b",
      name: "Force B · Rowing",
      description: "Rowing lourd en mouvement principal",
      exerciseIds: ["lowrow", "row", "pulldown", "curl", "abs"],
      mode: "strength",
    },
    {
      id: "pull-muscle-c",
      name: "Muscle C · Complet",
      description: "Largeur, épaisseur et arrière d’épaule",
      exerciseIds: ["pulldown", "row", "lowrow", "rear", "hammer-curl", "abs"],
      mode: "hypertrophy",
    },
    {
      id: "pull-mixed-b",
      name: "Mixte B · Unilatéral",
      description: "Tirages unilatéraux et contrôle",
      exerciseIds: ["lowrow", "pulldown", "row", "rear", "curl", "abs"],
      mode: "balanced",
    },
    {
      id: "pull-short",
      name: "Rapide · Essentiels",
      description: "Cinq exercices dos et biceps",
      exerciseIds: ["pulldown", "lowrow", "rear", "curl", "abs"],
      mode: "short",
    },
  ],
  legs: [
    {
      id: "legs-balanced",
      name: "Équilibré",
      description: "Quadriceps, ischios, fessiers et mollets",
      mode: "balanced",
    },
    {
      id: "legs-strength",
      name: "Force",
      description: "Squat et chaîne postérieure lourds",
      exerciseIds: ["squat", "rdl", "legcurl", "glute", "calf"],
      mode: "strength",
    },
    {
      id: "legs-quads",
      name: "Quadriceps prioritaires",
      description: "Accent squat et extension",
      exerciseIds: ["squat", "legext", "glute", "legcurl", "calf", "abduction"],
      mode: "hypertrophy",
    },
    {
      id: "legs-posterior",
      name: "Muscle B · Postérieure",
      description: "Accent chaîne postérieure",
      exerciseIds: ["rdl", "glute", "legcurl", "squat", "calf", "abduction"],
      mode: "hypertrophy",
    },
    {
      id: "legs-strength-b",
      name: "Force B · Charnière",
      description: "Soulevé roumain et fessiers lourds",
      exerciseIds: ["rdl", "glute", "squat", "legcurl", "calf"],
      mode: "strength",
    },
    {
      id: "legs-muscle-c",
      name: "Muscle C · Complète",
      description: "Volume réparti sur toutes les jambes",
      exerciseIds: ["squat", "rdl", "legcurl", "legext", "glute", "calf"],
      mode: "hypertrophy",
    },
    {
      id: "legs-mixed-b",
      name: "Mixte B · Machines",
      description: "Travail guidé et fatigue maîtrisée",
      exerciseIds: ["squat", "legcurl", "glute", "legext", "calf", "abduction"],
      mode: "balanced",
    },
    {
      id: "legs-short",
      name: "Rapide · Essentiels",
      description: "Cinq mouvements pour toutes les jambes",
      exerciseIds: ["squat", "rdl", "legcurl", "legext", "calf"],
      mode: "short",
    },
  ],
  upper: [
    {
      id: "upper-balanced",
      name: "Équilibré",
      description: "Haut du corps complet",
      mode: "balanced",
    },
    {
      id: "upper-strength",
      name: "Force",
      description: "Poussée et tirage lourds",
      exerciseIds: ["incline-db", "pulldown-u", "dip", "row-u", "pallof"],
      mode: "strength",
    },
    {
      id: "upper-hypertrophy",
      name: "Hypertrophie",
      description: "Volume réparti sur tout le haut",
      mode: "hypertrophy",
    },
    {
      id: "upper-short",
      name: "Rapide A · Essentiels",
      description: "Cinq exercices essentiels",
      exerciseIds: ["incline-db", "pulldown-u", "row-u", "lateral-u", "pallof"],
      mode: "short",
    },
    {
      id: "upper-strength-b",
      name: "Force B · Tirage",
      description: "Dos lourd, poussée complémentaire",
      exerciseIds: ["row-u", "pulldown-u", "incline-db", "dip", "pallof"],
      mode: "strength",
    },
    {
      id: "upper-hypertrophy-b",
      name: "Muscle B · Bras",
      description: "Haut complet avec accent bras",
      exerciseIds: [
        "incline-db",
        "pulldown-u",
        "row-u",
        "biceps-u",
        "triceps-u",
        "lateral-u",
      ],
      mode: "hypertrophy",
    },
  ],
  arms: [
    {
      id: "arms-balanced",
      name: "Équilibré",
      description: "Biceps, triceps et épaules",
      mode: "balanced",
    },
    {
      id: "arms-priority",
      name: "Bras prioritaires",
      description: "Deux mouvements biceps et triceps",
      exerciseIds: [
        "curl-a",
        "hammer-a",
        "rope-a",
        "overhead-a",
        "lateral-a",
        "abs-a",
      ],
      mode: "hypertrophy",
    },
    {
      id: "shoulders-priority",
      name: "Épaules prioritaires",
      description: "Deltoïdes latéraux et postérieurs",
      exerciseIds: ["lateral-a", "rear-a", "curl-a", "rope-a", "abs-a"],
      mode: "hypertrophy",
    },
    {
      id: "arms-short",
      name: "Rapide A · Essentiels",
      description: "Quatre exercices ciblés",
      exerciseIds: ["lateral-a", "curl-a", "rope-a", "abs-a"],
      mode: "short",
    },
    {
      id: "arms-balanced-b",
      name: "Équilibré B · Unilatéral",
      description: "Alternance biceps, triceps et épaules",
      exerciseIds: [
        "rear-a",
        "hammer-a",
        "overhead-a",
        "lateral-a",
        "curl-a",
        "rope-a",
      ],
      mode: "balanced",
    },
    {
      id: "arms-short-b",
      name: "Rapide B · Supersets",
      description: "Bras et épaules en cinq mouvements",
      exerciseIds: ["rear-a", "hammer-a", "overhead-a", "lateral-a", "abs-a"],
      mode: "short",
    },
  ],
};
const cv = (
  id: string,
  name: string,
  description: string,
  exerciseIds: string[],
  mode: Variant["mode"],
): Variant => ({ id, name, description, exerciseIds, mode });
const coachingVariants: Record<string, Variant[]> = {
  push: [
    cv(
      "pa",
      "A · Pectoraux équilibrés",
      "Deux angles de développé, puis épaules et triceps sans redondance",
      ["bench", "incline", "cable-fly", "shoulder-db", "lateral", "rope"],
      "strength",
    ),
    cv(
      "pb",
      "B · Haut des pectoraux",
      "Développé incliné prioritaire et poussée complète",
      ["incline", "bench-db", "cable-fly", "shoulder", "lateral-db", "overhead-a"],
      "hypertrophy",
    ),
    cv(
      "pc",
      "C · Épaules prioritaires",
      "Deltoïdes prioritaires avec un volume pectoraux maîtrisé",
      [
        "shoulder",
        "incline-bar",
        "lf-chest-press",
        "cable-fly",
        "lateral-u",
        "pushdown-bar",
      ],
      "hypertrophy",
    ),
    cv(
      "pd",
      "D · Machines",
      "Appuis stables, amplitude contrôlée et fatigue articulaire limitée",
      ["lf-chest-press", "incline", "fly", "shoulder", "lateral", "rope"],
      "balanced",
    ),
  ],
  pull: [
    cv(
      "pua",
      "A · Dos complet",
      "Largeur, épaisseur, arrière d’épaule et biceps",
      ["pulldown", "lowrow", "rear", "pullover", "curl", "abs"],
      "strength",
    ),
    cv(
      "pub",
      "B · Largeur du dos",
      "Développer le grand dorsal et la silhouette en V",
      ["pulldown", "pullup", "row-db", "pullover", "rear", "curl-cable"],
      "hypertrophy",
    ),
    cv(
      "puc",
      "C · Épaisseur du dos",
      "Développer rhomboïdes, trapèzes et densité dorsale",
      ["row", "cable-row", "pulldown", "rear", "hammer-curl", "backext"],
      "hypertrophy",
    ),
    cv(
      "pud",
      "D · Dos complet",
      "Équilibrer largeur, épaisseur, arrière d’épaule et bras",
      ["pulldown", "lowrow", "row-db", "facepull", "preacher", "abs"],
      "balanced",
    ),
  ],
  legs: [
    cv(
      "la",
      "A · Jambes équilibrées",
      "Quadriceps, ischios, fessiers et mollets dans une séance complète",
      ["squat", "rdl", "legcurl", "legext", "calf"],
      "strength",
    ),
    cv(
      "lb",
      "B · Quadriceps",
      "Développer les quadriceps avec travail unilatéral",
      ["squat", "legpress", "split-squat", "legcurl", "legext", "calf"],
      "hypertrophy",
    ),
    cv(
      "lc",
      "C · Ischios et fessiers",
      "Renforcer chaîne postérieure, hanches et lombaires",
      ["rdl", "hipthrust", "legcurl", "split-squat", "backext", "abduction"],
      "hypertrophy",
    ),
    cv(
      "ld",
      "D · Jambes complètes",
      "Volume équilibré avec fatigue systémique modérée",
      ["squat", "legpress", "legcurl", "legext", "glute", "calf"],
      "balanced",
    ),
  ],
  upper: [
    cv(
      "ua",
      "A · Haut du corps équilibré",
      "Deux poussées et deux tirages complémentaires",
      ["bench", "pulldown", "incline", "lowrow", "lateral", "curl-cable"],
      "strength",
    ),
    cv(
      "ub",
      "B · Haut du corps complet",
      "Pectoraux, dos, épaules et bras avec angles complémentaires",
      [
        "incline-db",
        "pulldown-u",
        "cable-row",
        "lateral-u",
        "biceps-u",
        "triceps-u",
      ],
      "hypertrophy",
    ),
    cv(
      "uc",
      "C · Dos prioritaire",
      "Renforcer le dos en maintenant la poussée",
      ["pulldown", "row-db", "lowrow", "bench-db", "lateral-db", "rope"],
      "hypertrophy",
    ),
    cv(
      "ud",
      "D · Équilibré",
      "Haut du corps complet avec fatigue maîtrisée",
      [
        "incline",
        "cable-row",
        "pulldown",
        "shoulder",
        "preacher",
        "pushdown-bar",
      ],
      "balanced",
    ),
  ],
  arms: [
    cv(
      "aa",
      "A · Bras équilibrés",
      "Biceps, triceps et deltoïdes en proportions égales",
      ["lateral-a", "curl-a", "rope-a", "rear-a", "hammer-a", "overhead-a"],
      "balanced",
    ),
    cv(
      "ab",
      "B · Biceps prioritaires",
      "Volume biceps avec maintien triceps et épaules",
      [
        "preacher",
        "curl-cable",
        "hammer-a",
        "lateral-db",
        "pushdown-bar",
        "rear-a",
      ],
      "hypertrophy",
    ),
    cv(
      "ac",
      "C · Triceps prioritaires",
      "Masse des triceps et stabilité des épaules",
      [
        "close-bench",
        "overhead-a",
        "rope-a",
        "lateral-u",
        "curl-a",
        "facepull",
      ],
      "hypertrophy",
    ),
    cv(
      "ad",
      "D · Épaules et bras",
      "Deltoïdes complets et bras sans fatigue excessive",
      [
        "shoulder-db",
        "lateral-a",
        "rear-a",
        "preacher",
        "pushdown-bar",
        "abs-a",
      ],
      "balanced",
    ),
  ],
};

const classicVariantCopy: Record<string, [string, string][]> = {
  push: [
    ["A", "Barre, presse inclinée, poulie, épaules et triceps"],
    ["B", "Presse inclinée, haltères, poulie et machines guidées"],
    ["C", "Développé épaules, incliné, chest press et isolations"],
    ["D", "Chest press, presse inclinée et mouvements guidés"],
  ],
  pull: [
    ["A", "Tirage vertical, low row, pull-over, arrière d’épaule et biceps"],
    ["B", "Tractions, tirage vertical, rowing haltère et pull-over"],
    ["C", "Rowing guidé, poulie basse, tirage vertical et lombaires"],
    ["D", "Tirages guidés, rowing unilatéral, face pull et curl pupitre"],
  ],
  legs: [
    ["A", "Squat pendulaire, soulevé roumain, leg curl et mollets"],
    ["B", "Squat, presse, fente bulgare, leg curl et leg extension"],
    ["C", "Soulevé roumain, hip thrust, leg curl et fente bulgare"],
    ["D", "Squat, presse, leg curl, leg extension et Glute Drive"],
  ],
  upper: [
    ["A", "Deux développés, deux tirages, épaules et biceps"],
    ["B", "Haltères, tirages, élévations latérales et bras"],
    ["C", "Tirages variés, développé haltères, épaules et triceps"],
    ["D", "Presse inclinée, tirages guidés, épaules et bras"],
  ],
  arms: [
    ["A", "Élévations, arrière d’épaule, deux curls et deux extensions"],
    ["B", "Curl pupitre, poulie, marteau, triceps et arrière d’épaule"],
    ["C", "Développé serré, deux extensions, élévations et curl"],
    ["D", "Développé épaules, élévations, arrière d’épaule et bras"],
  ],
};
Object.entries(classicVariantCopy).forEach(([category, copy]) =>
  coachingVariants[category].forEach((variant, index) => {
    variant.name = copy[index][0];
    variant.description = copy[index][1];
    variant.mode = "balanced";
  }),
);
Object.assign(coachingVariants, {
  full: [
    cv(
      "fa",
      "A · Équilibré",
      "Un mouvement majeur par grande zone musculaire",
      ["legpress", "bench", "pulldown", "legcurl", "shoulder", "abs"],
      "balanced",
    ),
    cv(
      "fb",
      "B · Machines",
      "Corps entier sur machines guidées",
      ["squat", "incline", "lowrow", "legext", "glute", "lateral"],
      "balanced",
    ),
    cv(
      "fc",
      "C · Unilatéral",
      "Stabilité et équilibre droite-gauche",
      ["split-squat", "bench-db", "row-db", "legcurl", "shoulder-db", "pallof"],
      "balanced",
    ),
    cv(
      "fd",
      "D · Chaîne postérieure",
      "Dos, fessiers et ischios légèrement prioritaires",
      ["rdl", "incline", "pulldown", "hipthrust", "rear", "rope"],
      "balanced",
    ),
  ],
  "chest-biceps": [
    cv(
      "cba",
      "A · Équilibré",
      "Trois angles pectoraux et deux angles de curl",
      ["bench", "incline", "fly", "curl", "preacher", "abs"],
      "balanced",
    ),
    cv(
      "cbb",
      "B · Incliné",
      "Haut des pectoraux prioritaire et biceps complets",
      [
        "incline-db",
        "bench-db",
        "cable-fly",
        "curl-cable",
        "hammer-curl",
        "abs",
      ],
      "balanced",
    ),
    cv(
      "cbc",
      "C · Machines",
      "Pectoraux guidés et biceps avec exécution contrôlée",
      ["lf-chest-press", "incline", "fly", "biceps-u", "preacher", "plank"],
      "balanced",
    ),
    cv(
      "cbd",
      "D · Haltères et poulie",
      "Angles libres pour varier les contraintes",
      ["bench-db", "incline-db", "cable-fly", "curl", "curl-cable", "pallof"],
      "balanced",
    ),
  ],
  "back-triceps": [
    cv(
      "bta",
      "A · Dos complet",
      "Largeur, épaisseur et deux angles triceps",
      ["pulldown", "lowrow", "row", "rope", "overhead-a", "abs"],
      "balanced",
    ),
    cv(
      "btb",
      "B · Largeur",
      "Tirages verticaux prioritaires et triceps",
      [
        "pullup",
        "pulldown",
        "pullover",
        "pushdown-bar",
        "close-bench",
        "plank",
      ],
      "balanced",
    ),
    cv(
      "btc",
      "C · Épaisseur",
      "Rowings prioritaires et extensions de triceps",
      ["cable-row", "row-db", "lowrow", "rope", "overhead-a", "backext"],
      "balanced",
    ),
    cv(
      "btd",
      "D · Machines",
      "Tirages guidés avec travail complet des triceps",
      ["row", "pulldown", "rear", "dip", "pushdown-bar", "abs"],
      "balanced",
    ),
  ],
  "chest-back": [
    cv(
      "cda",
      "A · Équilibré",
      "Alternance poussée et tirage pour tout le torse",
      ["bench", "pulldown", "incline", "lowrow", "fly", "rear"],
      "balanced",
    ),
    cv(
      "cdb",
      "B · Haut du torse",
      "Pectoraux supérieurs, largeur du dos et posture",
      [
        "incline-db",
        "pullup",
        "cable-fly",
        "cable-row",
        "pullover",
        "facepull",
      ],
      "balanced",
    ),
    cv(
      "cdc",
      "C · Machines",
      "Pectoraux et dos avec appuis stables",
      ["incline", "row", "fly", "pulldown", "lowrow", "rear"],
      "balanced",
    ),
    cv(
      "cdd",
      "D · Unilatéral",
      "Travail droite-gauche et amplitude contrôlée",
      ["bench-db", "row-db", "incline-db", "lowrow", "cable-fly", "pullover"],
      "balanced",
    ),
  ],
  "quads-calves": [
    cv(
      "qca",
      "A · Squat",
      "Quadriceps complets et mollets",
      ["squat", "legpress", "legext", "split-squat", "calf", "abs"],
      "balanced",
    ),
    cv(
      "qcb",
      "B · Presse",
      "Volume quadriceps avec fatigue dorsale réduite",
      ["legpress", "squat", "legext", "split-squat", "calf", "plank"],
      "balanced",
    ),
    cv(
      "qcc",
      "C · Unilatéral",
      "Équilibre des jambes et stabilité du bassin",
      ["split-squat", "legpress", "legext", "squat", "calf", "abduction"],
      "balanced",
    ),
    cv(
      "qcd",
      "D · Machines",
      "Quadriceps et mollets avec mouvements guidés",
      ["squat", "legext", "legpress", "calf", "abduction", "abs"],
      "balanced",
    ),
  ],
  "hams-glutes": [
    cv(
      "hga",
      "A · Équilibré",
      "Ischios, fessiers et stabilité des hanches",
      ["rdl", "legcurl", "glute", "backext", "abduction", "calf"],
      "balanced",
    ),
    cv(
      "hgb",
      "B · Fessiers",
      "Extension de hanche et moyen fessier prioritaires",
      ["hipthrust", "split-squat", "legcurl", "glute", "abduction", "abs"],
      "balanced",
    ),
    cv(
      "hgc",
      "C · Ischios",
      "Flexion du genou et charnière de hanche",
      ["rdl", "legcurl", "backext", "hipthrust", "calf", "plank"],
      "balanced",
    ),
    cv(
      "hgd",
      "D · Machines",
      "Chaîne postérieure avec exécution stable",
      ["glute", "legcurl", "abduction", "backext", "calf", "abs"],
      "balanced",
    ),
  ],
} satisfies Record<string, Variant[]>);

function buildVariant(base: Session, variant: Variant): Session {
  const chosen = variant.exerciseIds
    ? variant.exerciseIds
        .map((id) => allExercises.find((e) => e.id === id))
        .filter((e): e is Exercise => !!e)
    : base.exercises;
  const exercises = chosen.map((source) => {
    const e = exerciseCatalog.find((item) => normalizeExerciseName(item.name) === normalizeExerciseName(source.name)) || source;
    const highRepIsolation = /lateral|rear|facepull|abduction/.test(e.id);
    const standardIsolation = /fly|curl|biceps|rope|overhead|pushdown|legcurl|legext|pullover|calf|abs|backext/.test(e.id);
    const compound = /bench|incline|shoulder|dip|squat|legpress|rdl|pulldown|pullup|lowrow|row|hipthrust|glute|split/.test(e.id);
    return {
      ...e,
      target: e.id.includes("plank")
        ? "4 × 30–45 s"
        : highRepIsolation
          ? "4 × 12–20"
          : standardIsolation
            ? "4 × 10–15"
            : "4 × 8–12",
      rest: compound ? "2–3 min" : "1–2 min",
    };
  });
  return {
    ...base,
    id: variant.id,
    name: `${base.name} · ${variant.name}`,
    kicker: variant.description,
    exercises,
  };
}
function sessionForVariant(id: string) {
  for (const base of sessions) {
    const v = coachingVariants[base.id]?.find((x) => x.id === id);
    if (v) return buildVariant(base, v);
  }
  return null;
}
const techniqueNames: Record<string, string> = {
  incline: "Développé incliné guidé",
  fly: "Écarté assis guidé",
  shoulder: "Développé épaules guidé",
  lateral: "Élévation latérale guidée",
  pulldown: "Tirage vertical",
  lowrow: "Tirage horizontal bas",
  row: "Tirage horizontal",
  rear: "Oiseau guidé",
  abs: "Crunch guidé",
  squat: "Squat pendulaire",
  legcurl: "Leg curl assis",
  glute: "Hip thrust guidé",
  legext: "Extension des jambes",
  calf: "Élévation des mollets assis",
  abduction: "Abduction de hanches",
  dip: "Dips guidés",
  "pulldown-u": "Tirage vertical",
  "row-u": "Tirage horizontal",
  "biceps-u": "Curl biceps guidé",
  "lateral-a": "Élévation latérale guidée",
  "rear-a": "Oiseau guidé",
  "abs-a": "Crunch guidé",
};
const muscleNames: Record<string, string> = {
  bench: "Pectoraux · Triceps",
  incline: "Haut des pectoraux · Triceps · Épaules",
  fly: "Pectoraux",
  shoulder: "Épaules · Triceps",
  lateral: "Deltoïde latéral",
  pulldown: "Grand dorsal · Biceps",
  lowrow: "Grand dorsal · Trapèzes · Biceps",
  row: "Dos · Biceps",
  rear: "Deltoïde postérieur · Trapèzes",
  squat: "Quadriceps · Fessiers",
  rdl: "Ischio-jambiers · Fessiers · Lombaires",
  legcurl: "Ischio-jambiers",
  glute: "Fessiers · Ischio-jambiers",
  legext: "Quadriceps",
  calf: "Mollets",
  abduction: "Moyen fessier",
  dip: "Triceps · Pectoraux",
  plank: "Sangle abdominale",
};
const technique = (e: Exercise) => techniqueNames[e.id] || e.name,
  muscles = (e: Exercise) =>
    muscleNames[e.id] || muscleNames[e.id.replace(/-u|-a/, "")] || e.group,
  normalizeSearch = (value: string) =>
    value
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLocaleLowerCase("fr"),
  isOneAirMachine = (e: Exercise) => /Hammer|Life Fitness/.test(e.name),
  oneAirLabel = (e: Exercise) => (isOneAirMachine(e) ? e.name : e.alt);
const gymsFor = (exercise: Exercise): Gym[] => exercise.gyms || [exercise.gym || "Toutes"];
const restSecondsFor = (exercise: Exercise) => {
  const clock = exercise.rest.match(/^(\d+):(\d{2})$/);
  if (clock) return Number(clock[1]) * 60 + Number(clock[2]);
  if (/3 min/.test(exercise.rest)) return 180;
  if (/2–3|min 30–2/.test(exercise.rest)) return 150;
  if (/2 min/.test(exercise.rest)) return 120;
  if (/1 min 30/.test(exercise.rest)) return 90;
  return 60;
};
const formatTimer = (seconds: number) =>
  `${Math.floor(Math.max(0, seconds) / 60)}:${String(Math.max(0, seconds) % 60).padStart(2, "0")}`;
function Sparkline({ values }: { values: number[] }) {
  if (values.length < 2)
    return (
      <div className="chart-empty">
        Enregistre au moins 2 séances pour voir la courbe.
      </div>
    );
  const max = Math.max(...values),
    min = Math.min(...values),
    range = max - min || 1;
  const pts = values
    .map(
      (v, i) =>
        `${12 + i * (276 / (values.length - 1))},${100 - ((v - min) / range) * 72}`,
    )
    .join(" ");
  return (
    <svg className="spark" viewBox="0 0 300 120">
      <polyline
        points={pts}
        fill="none"
        stroke="#b7ef48"
        strokeWidth="4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      {values.map((v, i) => (
        <circle
          key={i}
          cx={12 + i * (276 / (values.length - 1))}
          cy={100 - ((v - min) / range) * 72}
          r="4"
          fill="#101510"
          stroke="#b7ef48"
          strokeWidth="3"
        />
      ))}
    </svg>
  );
}
const targetedCategoryIds = new Set([
  "chest-biceps",
  "back-triceps",
  "chest-back",
  "quads-calves",
  "hams-glutes",
]);
const progressAreas = [
  "Pectoraux",
  "Dos",
  "Épaules",
  "Biceps",
  "Triceps",
  "Quadriceps",
  "Ischios / fessiers",
];
function programNameForLog(sessionId: string) {
  if (sessionId === "free") return "Séance libre";
  const direct = sessions.find((session) => session.id === sessionId);
  if (direct) return direct.name;
  for (const session of sessions) {
    if (coachingVariants[session.id]?.some((variant) => variant.id === sessionId))
      return session.name;
  }
  return "Séance libre";
}
function progressAreaFor(exercise: Exercise) {
  if (/rdl|legcurl|glute|hipthrust|backext|abduction/.test(exercise.id))
    return "Ischios / fessiers";
  if (/squat|legpress|legext|split|goblet/.test(exercise.id))
    return "Quadriceps";
  if (exercise.group === "Jambes" || exercise.group === "Mollets")
    return "Quadriceps";
  if (/Ischio|Fessier|Lombaire/.test(exercise.group))
    return "Ischios / fessiers";
  return progressAreas.includes(exercise.group) ? exercise.group : null;
}
export default function Home() {
  const [tab, setTab] = useState("home"),
    [logs, setLogs] = useState<Log[]>([]),
    [active, setActive] = useState<Session | null>(null),
    [pendingSession, setPendingSession] = useState<Session | null>(null),
    [draft, setDraft] = useState<Record<string, SetRow[]>>({}),
    [started, setStarted] = useState(Date.now()),
    [selected, setSelected] = useState("bench"),
    [programCategory, setProgramCategory] = useState("push"),
    [selectedVariant, setSelectedVariant] = useState("pa"),
    [programFamily, setProgramFamily] = useState<"classic" | "targeted">(
      "classic",
    ),
    [theme, setTheme] = useState("dark"),
    [oneAir, setOneAir] = useState(true),
    [workoutGym, setWorkoutGym] = useState<Gym>("One Air"),
    [exerciseGyms, setExerciseGyms] = useState<Record<string, Gym>>({}),
    [weekPlan, setWeekPlan] = useState<string[]>([]),
    [customExercises, setCustomExercises] = useState<Exercise[]>([]),
    [hiddenExerciseIds, setHiddenExerciseIds] = useState<string[]>([]),
    [catalogSearch, setCatalogSearch] = useState(""),
    [catalogGym, setCatalogGym] = useState<"Toutes" | "One Air" | "Basic-Fit">("Toutes"),
    [pickerGym, setPickerGym] = useState<"Toutes" | "One Air" | "Basic-Fit">("Toutes"),
    [exerciseSearch, setExerciseSearch] = useState(""),
    [replaceExerciseId, setReplaceExerciseId] = useState<string | null>(null),
    [addExerciseOpen, setAddExerciseOpen] = useState(false),
    [editingExerciseId, setEditingExerciseId] = useState<string | null>(null),
    [exerciseEdit, setExerciseEdit] = useState<ExerciseEdit | null>(null),
    [editingFromCatalog, setEditingFromCatalog] = useState(false),
    [workoutHistoryOpen, setWorkoutHistoryOpen] = useState(false),
    [restDurations, setRestDurations] = useState<Record<string, number>>({}),
    [restEnd, setRestEnd] = useState<number | null>(null),
    [restExerciseId, setRestExerciseId] = useState<string | null>(null),
    [now, setNow] = useState(Date.now()),
    [ready, setReady] = useState(false),
    [toast, setToast] = useState("");
  useEffect(() => {
    try {
      const raw = localStorage.getItem("jo-strength-logs");
      if (raw) setLogs(JSON.parse(raw));
      setTheme(localStorage.getItem("training-theme") || "dark");
      setOneAir(localStorage.getItem("training-one-air") !== "false");
      setWorkoutGym((localStorage.getItem("training-workout-gym") as Gym) || (localStorage.getItem("training-one-air") === "false" ? "Toutes" : "One Air"));
      const wp = localStorage.getItem("training-week-plan");
      if (wp) setWeekPlan(JSON.parse(wp));
      const custom = localStorage.getItem("training-custom-exercises");
      if (custom) setCustomExercises(JSON.parse(custom));
      const hidden = localStorage.getItem("training-hidden-exercises");
      if (hidden) setHiddenExerciseIds(JSON.parse(hidden));
      const current = localStorage.getItem("jo-strength-active");
      if (current) {
        const saved = JSON.parse(current);
        if (saved.active && saved.draft) {
          setActive(saved.active);
          setDraft(saved.draft);
          setStarted(saved.started || Date.now());
          setRestDurations(saved.restDurations || {});
          setRestEnd(saved.restEnd || null);
          setRestExerciseId(saved.restExerciseId || null);
          setWorkoutGym(saved.workoutGym || "One Air");
          setExerciseGyms(saved.exerciseGyms || Object.fromEntries(saved.active.exercises.map((exercise: Exercise) => [exercise.id, saved.workoutGym || "One Air"])));
          setTab("workout");
        }
      }
    } catch {}
    if ("serviceWorker" in navigator)
      navigator.serviceWorker.register("/sw.js").catch(() => {});
    setReady(true);
  }, []);
  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    if (ready) localStorage.setItem("training-theme", theme);
  }, [theme, ready]);
  useEffect(() => {
    if (ready) localStorage.setItem("training-one-air", String(oneAir));
  }, [oneAir, ready]);
  useEffect(() => {
    if (ready) localStorage.setItem("training-workout-gym", workoutGym);
    setOneAir(workoutGym === "One Air");
  }, [workoutGym, ready]);
  useEffect(() => {
    if (ready)
      localStorage.setItem("training-week-plan", JSON.stringify(weekPlan));
  }, [weekPlan, ready]);
  useEffect(() => {
    if (ready)
      localStorage.setItem(
        "training-custom-exercises",
        JSON.stringify(customExercises),
      );
  }, [customExercises, ready]);
  useEffect(() => {
    if (ready)
      localStorage.setItem("training-hidden-exercises", JSON.stringify(hiddenExerciseIds));
  }, [hiddenExerciseIds, ready]);
  useEffect(() => {
    if (ready) localStorage.setItem("jo-strength-logs", JSON.stringify(logs));
  }, [logs, ready]);
  useEffect(() => {
    if (!ready) return;
    if (active)
      localStorage.setItem(
        "jo-strength-active",
        JSON.stringify({
          active,
          draft,
          started,
          restDurations,
          restEnd,
          restExerciseId,
          workoutGym,
          exerciseGyms,
        }),
      );
    else localStorage.removeItem("jo-strength-active");
  }, [active, draft, started, restDurations, restEnd, restExerciseId, workoutGym, exerciseGyms, ready]);
  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [active]);
  useEffect(() => {
    if (restEnd && now >= restEnd) {
      setRestEnd(null);
      setToast("Repos terminé ✓");
      if (navigator.vibrate) navigator.vibrate([180, 100, 180]);
    }
  }, [now, restEnd]);
  useEffect(() => {
    if (toast) {
      const t = setTimeout(() => setToast(""), 2400);
      return () => clearTimeout(t);
    }
  }, [toast]);
  const historyKey = (id: string, gym: Gym = workoutGym) => `${id}@@${gym}`,
    lastSetsFor = (id: string, gym: Gym = workoutGym) =>
      [...logs]
        .reverse()
        .map((l) => {
          const source = [...allExercises, ...customExercises].find((exercise) => exercise.id === id);
          const aliases = source
            ? [...allExercises, ...customExercises].filter((exercise) => normalizeExerciseName(exercise.name) === normalizeExerciseName(source.name)).map((exercise) => exercise.id)
            : [id];
          return aliases.map((alias) => l.entries[historyKey(alias, gym)] || (gym === "One Air" ? l.entries[alias] : undefined)).find((rows) => (rows || []).some((row) => row.weight || row.reps));
        })
        .find((rows) => (rows || []).some((x) => x.weight || x.reps)) || [],
    lastFor = (id: string) =>
      lastSetsFor(id)
        .filter((x) => x.weight || x.reps)
        .at(-1);
  function start(s: Session) {
    const next: Record<string, SetRow[]> = {};
    s.exercises.forEach((e) => {
      const previous = lastSetsFor(e.id);
      next[e.id] = Array.from({ length: e.sets }, (_, i) => ({
        weight: previous[i]?.weight || previous.at(-1)?.weight || "",
        reps: "",
        rir: "",
      }));
    });
    setDraft(next);
    setStarted(Date.now());
    setNow(Date.now());
    setRestDurations(
      Object.fromEntries(s.exercises.map((e) => [e.id, restSecondsFor(e)])),
    );
    setRestEnd(null);
    setRestExerciseId(null);
    setExerciseGyms(Object.fromEntries(s.exercises.map((exercise) => [exercise.id, gymsFor(exercise).includes(workoutGym) || gymsFor(exercise).includes("Toutes") ? workoutGym : gymsFor(exercise)[0]])));
    setActive(s);
    setTab("workout");
    scrollTo(0, 0);
  }
  function requestStart(s: Session) {
    setPendingSession(s);
  }
  function confirmStart() {
    if (!pendingSession) return;
    const session = pendingSession;
    setPendingSession(null);
    start(session);
  }
  function startFreeWorkout() {
    requestStart({
      id: "free",
      name: "Séance libre",
      kicker: "Exercices choisis à la volée",
      color: "#b7ef48",
      exercises: [],
    });
  }
  function addExercise(exercise: Exercise) {
    if (!active || active.exercises.some((item) => item.id === exercise.id)) {
      setToast("Cet exercice est déjà dans la séance");
      return;
    }
    const previous = lastSetsFor(exercise.id);
    setActive({ ...active, exercises: [...active.exercises, exercise] });
    setDraft((current) => ({
      ...current,
      [exercise.id]: Array.from({ length: exercise.sets }, (_, index) => ({
        weight: previous[index]?.weight || previous.at(-1)?.weight || "",
        reps: "",
        rir: "",
      })),
    }));
    setExerciseSearch("");
    setAddExerciseOpen(false);
    setRestDurations((current) => ({
      ...current,
      [exercise.id]: restSecondsFor(exercise),
    }));
    setExerciseGyms((current) => ({ ...current, [exercise.id]: gymsFor(exercise).includes(workoutGym) || gymsFor(exercise).includes("Toutes") ? workoutGym : gymsFor(exercise)[0] }));
    setToast("Exercice ajouté ✓");
  }
  function openExerciseEditor(exercise: Exercise) {
    setEditingFromCatalog(false);
    setEditingExerciseId(exercise.id);
    setExerciseEdit({
      name: exercise.name,
      group: exercise.group,
      sets: exercise.sets,
      target: exercise.target,
      restSeconds: restDurations[exercise.id] || restSecondsFor(exercise),
      link: exercise.link || "",
      tags: (exercise.tags?.length ? exercise.tags : inferredTags(exercise)).join(", "),
      gyms: exercise.gyms || [exercise.gym || (/Hammer|Life Fitness/.test(exercise.name) ? "One Air" : "Toutes")],
    });
  }
  function openCatalogEditor(exercise: Exercise) {
    setEditingFromCatalog(true);
    setEditingExerciseId(exercise.id);
    setExerciseEdit({ name: exercise.name, group: exercise.group, sets: exercise.sets, target: exercise.target, restSeconds: restSecondsFor(exercise), link: exercise.link || "", tags: (exercise.tags?.length ? exercise.tags : inferredTags(exercise)).join(", "), gyms: exercise.gyms || [exercise.gym || (/Hammer|Life Fitness/.test(exercise.name) ? "One Air" : "Toutes")] });
  }
  function openExerciseCreator() {
    setAddExerciseOpen(false);
    setReplaceExerciseId(null);
    setExerciseSearch("");
    setEditingExerciseId("__new__");
    setEditingFromCatalog(false);
    setExerciseEdit({ name: "", group: "", sets: 4, target: "4 × 8–12", restSeconds: 90, link: "", tags: "", gyms: [workoutGym] });
  }
  function openCatalogCreator() {
    setEditingFromCatalog(true);
    setEditingExerciseId("__new__");
    setExerciseEdit({ name: "", group: "", sets: 4, target: "4 × 8–12", restSeconds: 90, link: "", tags: "", gyms: [catalogGym] });
  }
  function toggleExerciseTag(tag: string) {
    if (!exerciseEdit) return;
    const current = exerciseEdit.tags.split(",").map((item) => item.trim()).filter(Boolean);
    const next = current.some((item) => normalizeExerciseName(item) === normalizeExerciseName(tag))
      ? current.filter((item) => normalizeExerciseName(item) !== normalizeExerciseName(tag))
      : [...current, tag];
    setExerciseEdit({ ...exerciseEdit, tags: next.join(", ") });
  }
  function useExistingExercise(exercise: Exercise) {
    if (active && !editingFromCatalog) addExercise(exercise);
    else {
      setEditingExerciseId(null);
      setExerciseEdit(null);
      setToast("Exercice existant sélectionné ✓");
    }
  }
  function saveExerciseEdit() {
    if (!editingExerciseId || !exerciseEdit) return;
    const sets = Math.max(1, Math.min(10, Number(exerciseEdit.sets) || 4));
    if (editingExerciseId === "__new__") {
      if (!exerciseEdit.name.trim() || !exerciseEdit.group.trim()) {
        setToast("Indique le nom et le muscle");
        return;
      }
      const exactDuplicate = managedExerciseCatalog.find((exercise) => normalizeExerciseName(exercise.name) === normalizeExerciseName(exerciseEdit.name));
      if (exactDuplicate) {
        setToast(`« ${exactDuplicate.name} » existe déjà`);
        return;
      }
      const created: Exercise = {
        id: `custom-${Date.now()}`,
        name: exerciseEdit.name.trim(),
        group: exerciseEdit.group.trim(),
        sets,
        target: exerciseEdit.target.trim() || "4 × 8–12",
        rest: formatTimer(exerciseEdit.restSeconds),
        link: exerciseEdit.link.trim() || undefined,
        tags: exerciseEdit.tags.split(",").map((tag) => tag.trim()).filter(Boolean),
        gyms: exerciseEdit.gyms.length ? exerciseEdit.gyms : ["Toutes"],
      };
      setCustomExercises((current) => [...current, created]);
      if (active && !editingFromCatalog) {
        setActive({ ...active, exercises: [...active.exercises, created] });
        setDraft((current) => ({ ...current, [created.id]: Array.from({ length: sets }, () => ({ weight: "", reps: "", rir: "" })) }));
        setRestDurations((current) => ({ ...current, [created.id]: exerciseEdit.restSeconds }));
        setExerciseGyms((current) => ({ ...current, [created.id]: exerciseEdit.gyms.includes(workoutGym) || exerciseEdit.gyms.includes("Toutes") ? workoutGym : exerciseEdit.gyms[0] || workoutGym }));
      }
      setEditingExerciseId(null);
      setExerciseEdit(null);
      setToast(editingFromCatalog ? "Exercice ajouté au catalogue ✓" : "Nouvel exercice créé ✓");
      return;
    }
    const updated = (item: Exercise): Exercise => ({ ...item, name: exerciseEdit.name.trim() || item.name, group: exerciseEdit.group.trim() || item.group, sets, target: exerciseEdit.target.trim() || item.target, rest: formatTimer(exerciseEdit.restSeconds), link: exerciseEdit.link.trim() || undefined, tags: exerciseEdit.tags.split(",").map((tag) => tag.trim()).filter(Boolean), gyms: exerciseEdit.gyms.length ? exerciseEdit.gyms : ["Toutes"] });
    if (active) setActive({
      ...active,
      exercises: active.exercises.map((item) =>
        item.id === editingExerciseId
          ? updated(item)
          : item,
      ),
    });
    if (active?.exercises.some((item) => item.id === editingExerciseId)) setDraft((current) => {
      const existing = current[editingExerciseId] || [];
      return {
        ...current,
        [editingExerciseId]: Array.from({ length: sets }, (_, index) =>
          existing[index] || { weight: "", reps: "", rir: "" },
        ),
      };
    });
    setRestDurations((current) => ({ ...current, [editingExerciseId]: exerciseEdit.restSeconds }));
    const baseExercise = customExercises.find((item) => item.id === editingExerciseId) || exerciseCatalog.find((item) => item.id === editingExerciseId);
    if (baseExercise) setCustomExercises((current) => {
      const replacement = updated(baseExercise);
      return current.some((item) => item.id === editingExerciseId) ? current.map((item) => item.id === editingExerciseId ? replacement : item) : [...current, replacement];
    });
    setEditingExerciseId(null);
    setExerciseEdit(null);
    setToast("Exercice modifié ✓");
  }
  function deleteCatalogExercise(id: string) {
    if (!confirm("Supprimer cet exercice du catalogue ?")) return;
    setCustomExercises((current) => current.filter((item) => item.id !== id));
    if (exerciseCatalog.some((item) => item.id === id)) setHiddenExerciseIds((current) => [...new Set([...current, id])]);
    setToast("Exercice supprimé du catalogue");
  }
  function replaceExercise(oldId: string, exercise: Exercise) {
    if (!active) return;
    if (
      active.exercises.some(
        (item) => item.id === exercise.id && item.id !== oldId,
      )
    ) {
      setToast("Cet exercice est déjà dans la séance");
      return;
    }
    const previous = lastSetsFor(exercise.id);
    setActive({
      ...active,
      exercises: active.exercises.map((item) =>
        item.id === oldId ? exercise : item,
      ),
    });
    setDraft((current) => {
      const next = { ...current };
      delete next[oldId];
      next[exercise.id] = Array.from({ length: exercise.sets }, (_, index) => ({
        weight: previous[index]?.weight || previous.at(-1)?.weight || "",
        reps: "",
        rir: "",
      }));
      return next;
    });
    setRestDurations((current) => {
      const next = { ...current };
      delete next[oldId];
      next[exercise.id] = restSecondsFor(exercise);
      return next;
    });
    setExerciseGyms((current) => {
      const next = { ...current };
      delete next[oldId];
      next[exercise.id] = gymsFor(exercise).includes(workoutGym) || gymsFor(exercise).includes("Toutes") ? workoutGym : gymsFor(exercise)[0];
      return next;
    });
    setReplaceExerciseId(null);
    setExerciseSearch("");
    setToast("Exercice remplacé ✓");
  }
  function removeExercise(exerciseId: string) {
    if (!active) return;
    setActive({
      ...active,
      exercises: active.exercises.filter((item) => item.id !== exerciseId),
    });
    setDraft((current) => {
      const next = { ...current };
      delete next[exerciseId];
      return next;
    });
    setExerciseGyms((current) => { const next = { ...current }; delete next[exerciseId]; return next; });
  }
  function moveWeek(i: number, d: number) {
    setWeekPlan((p) => {
      const n = i + d;
      if (n < 0 || n >= p.length) return p;
      const next = [...p];
      [next[i], next[n]] = [next[n], next[i]];
      return next;
    });
  }
  function startRestFor(exerciseId: string) {
    const exercise = active?.exercises.find((item) => item.id === exerciseId);
    const duration =
      restDurations[exerciseId] || (exercise ? restSecondsFor(exercise) : 90);
    setRestExerciseId(exerciseId);
    setRestEnd(Date.now() + duration * 1000);
    setNow(Date.now());
  }
  function switchExerciseGym(exerciseId: string, gym: Gym) {
    const hasEnteredSets = (draft[exerciseId] || []).some((row) => row.reps);
    if (hasEnteredSets && !confirm("Changer de salle pour cet exercice ? Les séries déjà saisies seront enregistrées dans la nouvelle salle.")) return;
    setExerciseGyms((current) => ({ ...current, [exerciseId]: gym }));
    if (!hasEnteredSets) {
      const previous = lastSetsFor(exerciseId, gym);
      setDraft((current) => ({ ...current, [exerciseId]: (current[exerciseId] || []).map((row, index) => ({ ...row, weight: previous[index]?.weight || previous.at(-1)?.weight || "" })) }));
    }
  }
  function update(id: string, i: number, key: keyof SetRow, value: string) {
    setDraft((d) => ({
      ...d,
      [id]: d[id].map((r, n) => (n === i ? { ...r, [key]: value } : r)),
    }));
  }
  function finish() {
    if (!active) return;
    if (!Object.values(draft).some((r) => r.some((x) => x.weight || x.reps))) {
      setToast("Ajoute au moins une série");
      return;
    }
    setLogs((x) => [
      ...x,
      {
        id: crypto.randomUUID(),
        date: new Date().toISOString(),
        sessionId: active.id,
        duration: Math.max(1, Math.round((Date.now() - started) / 60000)),
        entries: Object.fromEntries(Object.entries(draft).map(([exerciseId, rows]) => [historyKey(exerciseId, exerciseGyms[exerciseId] || workoutGym), rows])),
      },
    ]);
    setActive(null);
    setTab("home");
    setToast("Séance enregistrée ✓");
    scrollTo(0, 0);
  }
  const completedSets = Object.values(draft)
      .flat()
      .filter((r) => r.weight && r.reps).length,
    totalVolume = (l: Log) =>
      Object.values(l.entries)
        .flat()
        .reduce((a, r) => a + (+r.weight || 0) * (+r.reps || 0), 0),
    weekCount = logs.filter(
      (l) => Date.now() - new Date(l.date).getTime() < 604800000,
    ).length,
    monthVolume = logs
      .filter((l) => Date.now() - new Date(l.date).getTime() < 2592000000)
      .reduce((a, l) => a + totalVolume(l), 0);
  const history = useMemo(
    () =>
      logs
        .map((l) => l.entries[historyKey(selected)] || (workoutGym === "One Air" ? l.entries[selected] : []) || [])
        .filter((x) => x.length)
        .map((sets) => ({ max: Math.max(...sets.map((s) => +s.weight || 0)) })),
    [logs, selected],
  );
  const currentEx =
      allExercises.find((e) => e.id === selected) || allExercises[0],
    visibleVariants = coachingVariants[programCategory],
    visibleCategories = sessions.filter((s) =>
      programFamily === "targeted"
        ? targetedCategoryIds.has(s.id)
        : !targetedCategoryIds.has(s.id),
    ),
    managedExerciseCatalog = Array.from(
      [...exerciseCatalog, ...customExercises].reduce((catalog, exercise) => {
        if (hiddenExerciseIds.includes(exercise.id)) return catalog;
        const key = normalizeExerciseName(exercise.name);
        const existing = catalog.get(key);
        if (!existing) catalog.set(key, { ...exercise, tags: [...new Set([...(exercise.tags || []), ...inferredTags(exercise)])] });
        else catalog.set(key, { ...existing, tags: [...new Set([...(existing.tags || []), ...(exercise.tags || []), ...inferredTags(exercise)])], gyms: [...new Set([...gymsFor(existing), ...gymsFor(exercise)])] });
        return catalog;
      }, new Map<string, Exercise>()).values(),
    ),
    freeResults = managedExerciseCatalog
      .filter((exercise) => {
        const query = normalizeSearch(exerciseSearch.trim());
        if (!query) return false;
        return normalizeSearch(
          `${technique(exercise)} ${muscles(exercise)} ${exercise.name} ${(exercise.tags || []).join(" ")} ${gymsFor(exercise).join(" ")}`,
        ).includes(query);
      })
      .filter((exercise) => pickerGym === "Toutes" || (exercise.gyms || [exercise.gym || "Toutes"]).some((gym) => gym === pickerGym || gym === "Toutes"))
      .filter(
        (exercise) =>
          !active?.exercises.some((item) => item.id === exercise.id),
      )
      .slice(0, 10);
  const similarExercises = editingExerciseId === "__new__" && exerciseEdit && exerciseEdit.name.trim().length >= 3
    ? managedExerciseCatalog
        .map((exercise) => {
          const wanted = normalizeExerciseName(exerciseEdit.name);
          const existing = normalizeExerciseName(exercise.name);
          const wantedTokens = new Set(wanted.split(" ").filter((token) => token.length > 2));
          const existingTokens = new Set(existing.split(" ").filter((token) => token.length > 2));
          const common = [...wantedTokens].filter((token) => existingTokens.has(token)).length;
          const score = wanted === existing ? 1 : common / Math.max(wantedTokens.size, existingTokens.size, 1);
          return { exercise, score };
        })
        .filter(({ score }) => score >= 0.5)
        .sort((a, b) => b.score - a.score)
        .slice(0, 3)
    : [];
  const recentLogs = logs.filter(
      (log) => Date.now() - new Date(log.date).getTime() < 28 * 86400000,
    ),
    recentSetCount = recentLogs.reduce(
      (total, log) =>
        total +
        Object.values(log.entries)
          .flat()
          .filter((row) => row.weight || row.reps).length,
      0,
    ),
    averageDuration = recentLogs.length
      ? Math.round(
          recentLogs.reduce((total, log) => total + log.duration, 0) /
            recentLogs.length,
        )
      : 0,
    programCounts = Object.entries(
      recentLogs.reduce<Record<string, number>>((counts, log) => {
        const name = programNameForLog(log.sessionId);
        counts[name] = (counts[name] || 0) + 1;
        return counts;
      }, {}),
    ).sort((a, b) => b[1] - a[1]),
    areaCounts = recentLogs.reduce<Record<string, number>>(
      (counts, log) => {
        Object.entries(log.entries).forEach(([entryKey, rows]) => {
          const exerciseId = entryKey.split("@@")[0];
          const exercise = managedExerciseCatalog.find(
            (item) => item.id === exerciseId,
          );
          const area = exercise ? progressAreaFor(exercise) : null;
          if (!area) return;
          counts[area] =
            (counts[area] || 0) +
            rows.filter((row) => row.weight || row.reps).length;
        });
        return counts;
      },
      Object.fromEntries(progressAreas.map((area) => [area, 0])),
    ),
    maxProgramCount = Math.max(1, ...programCounts.map(([, count]) => count)),
    maxAreaCount = Math.max(1, ...Object.values(areaCounts)),
    neglectedAreas = progressAreas.filter((area) => areaCounts[area] === 0),
    lightAreas = progressAreas
      .filter((area) => areaCounts[area] > 0 && areaCounts[area] < maxAreaCount * 0.35)
      .slice(0, 3);
  const exerciseHistoryFor = (exerciseId: string, gym: Gym) => [...logs].reverse().map((log) => ({ date: log.date, rows: log.entries[historyKey(exerciseId, gym)] || (gym === "One Air" ? log.entries[exerciseId] : undefined) })).filter((item) => item.rows?.some((row) => row.weight || row.reps)).slice(0, 3);
  if (tab === "workout" && active)
    return (
      <main className="app workout-page">
        <header className="workout-head">
          <button
            className="icon-btn"
            onClick={() => setTab("home")}
            aria-label="Retour à l’accueil sans terminer la séance"
          >
            ‹
          </button>
          <div>
            <span>SÉANCE EN COURS</span>
            <h1>{active.name}</h1>
          </div>
          <div className="workout-head-actions">
            <button className="workout-history-button" onClick={() => { setTab("history"); scrollTo(0, 0); }} aria-label="Consulter l’historique">◷<span>Historique</span></button>
            <div className="session-clock">{formatTimer(Math.floor((now - started) / 1000))}</div>
          </div>
        </header>
        <div className="progress">
          <div
            style={{
              width: `${active.exercises.length ? Math.min(100, (completedSets / active.exercises.reduce((a, e) => a + e.sets, 0)) * 100) : 0}%`,
            }}
          />
        </div>
        <div className="workout-gym-select">
          <label htmlFor="workout-gym">Salle de cette séance</label>
          <select id="workout-gym" value={workoutGym} onChange={(event) => setWorkoutGym(event.target.value as Gym)}><option>One Air</option><option>Basic-Fit</option><option>Toutes</option></select>
        </div>
        {!replaceExerciseId && !addExerciseOpen && (
          <button className="add-exercise-trigger" onClick={() => { setAddExerciseOpen(true); setExerciseSearch(""); }}>
            <span>+</span> Ajouter un exercice
          </button>
        )}
        {active.id === "free" && !active.exercises.length && !addExerciseOpen && (
          <p className="empty-workout-hint">Ajoute ton premier exercice pour commencer.</p>
        )}
        {addExerciseOpen && !replaceExerciseId && (
          <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) { setAddExerciseOpen(false); setExerciseSearch(""); } }}>
          <section className="exercise-picker add-exercise-modal" role="dialog" aria-modal="true" aria-labelledby="add-exercise-title">
            <div className="picker-heading">
              <div><small>MODIFIER LA SÉANCE</small><h2 id="add-exercise-title">Ajouter un exercice</h2></div>
              <button aria-label="Fermer" onClick={() => { setAddExerciseOpen(false); setExerciseSearch(""); }}>×</button>
            </div>
            <div className="exercise-search">
              <span>⌕</span>
              <input
                id="exercise-search"
                type="search"
                value={exerciseSearch}
                placeholder="Pectoraux, tirage, curl, jambes…"
                onChange={(event) => setExerciseSearch(event.target.value)}
              />
            </div>
            <div className="gym-filter" aria-label="Filtrer par salle">
              {(["Toutes", "One Air", "Basic-Fit"] as const).map((gym) => <button key={gym} className={pickerGym === gym ? "active" : ""} onClick={() => setPickerGym(gym)}>{gym}</button>)}
            </div>
            {exerciseSearch && (
              <div className="exercise-results">
                {freeResults.length ? (
                  freeResults.map((exercise) => (
                    <button
                      key={exercise.id}
                      onClick={() => addExercise(exercise)}
                    >
                      <span>
                        <strong>{technique(exercise)}</strong>
                        <small>{muscles(exercise)} · 4 × 8–12</small>
                      </span>
                      <b>+</b>
                    </button>
                  ))
                ) : (
                  <p>Aucun exercice trouvé</p>
                )}
              </div>
            )}
            <button className="create-exercise-button" onClick={openExerciseCreator}>
              <span>＋</span><span><strong>Créer un nouvel exercice</strong><small>Machine absente ou exercice personnalisé</small></span>
            </button>
            {!exerciseSearch && (
              <p className="picker-empty">
                Recherche par nom, machine ou groupe musculaire.
              </p>
            )}
          </section>
          </div>
        )}
        <section className="workout-list">
          {active.exercises.map((e, n) => {
            const availableGyms: Gym[] = gymsFor(e).includes("Toutes") ? ["One Air", "Basic-Fit", "Toutes"] : gymsFor(e);
            const selectedGym = exerciseGyms[e.id] || (availableGyms.includes(workoutGym) ? workoutGym : availableGyms[0]);
            const previous = lastSetsFor(e.id, selectedGym);
            const gymHistory = exerciseHistoryFor(e.id, selectedGym);
            const record = Math.max(0, ...gymHistory.flatMap((item) => item.rows || []).map((row) => Number(row.weight) || 0));
            return (
              <article className="set-card" key={e.id}>
                <div className="set-title">
                  <div className="exercise-no">{n + 1}</div>
                  <div>
                    <h2>{technique(e)}</h2>
                    <p>
                      {muscles(e)} · {e.target} · repos {e.rest}
                    </p>
                    {oneAir && oneAirLabel(e) && (
                      <small className="machine-name">{oneAirLabel(e)}</small>
                    )}
                  </div>
                  {oneAir && e.link && (
                    <a href={e.link} target="_blank" rel="noreferrer">
                      ↗
                    </a>
                  )}
                </div>
                <div className="exercise-gym-history">
                  <label>Salle<select value={selectedGym} onChange={(event) => switchExerciseGym(e.id, event.target.value as Gym)}>{availableGyms.map((gym) => <option key={gym}>{gym}</option>)}</select></label>
                  <div><span>Record dans cette salle</span><strong>{record ? `${record} kg` : "—"}</strong></div>
                  {gymHistory.length > 0 && <details><summary>Historique {selectedGym}</summary>{gymHistory.map((item) => <p key={item.date}><b>{new Intl.DateTimeFormat("fr-FR", { day: "numeric", month: "short" }).format(new Date(item.date))}</b> {item.rows?.filter((row) => row.weight || row.reps).map((row) => `${row.weight || "—"}×${row.reps || "—"}`).join(" · ")}</p>)}</details>}
                </div>
                <div className="set-labels">
                  <span>SÉRIE</span>
                  <span>DERNIÈRE</span>
                  <span>KG</span>
                  <span>REPS</span>
                  <span>✓</span>
                </div>
                {draft[e.id]?.map((r, i) => (
                  <div
                    className={`set-row ${r.weight && r.reps ? "done" : ""}`}
                    key={i}
                  >
                    <b>{i + 1}</b>
                    <span className="last-cell">
                      {(previous[i]?.weight || "—") +
                        "×" +
                        (previous[i]?.reps || "—")}
                    </span>
                    <input
                      inputMode="decimal"
                      value={r.weight}
                      placeholder={
                        previous[i]?.weight || lastFor(e.id)?.weight || "—"
                      }
                      onChange={(x) =>
                        update(e.id, i, "weight", x.target.value)
                      }
                    />
                    <input
                      inputMode="numeric"
                      value={r.reps}
                      placeholder={previous[i]?.reps || "—"}
                      onChange={(event) => {
                        const value = event.target.value;
                        if (!r.reps && value) startRestFor(e.id);
                        update(e.id, i, "reps", value);
                      }}
                    />
                    <i>{r.weight && r.reps ? "✓" : ""}</i>
                  </div>
                ))}
                <div className="exercise-controls">
                  {restEnd && restExerciseId === e.id && (
                    <div className="mini-rest-timer">
                      <span>Repos</span>
                      <strong>
                        {formatTimer(Math.ceil((restEnd - now) / 1000))}
                      </strong>
                      <button onClick={() => setRestEnd(restEnd + 30000)}>
                        +30
                      </button>
                      <button
                        aria-label="Arrêter le timer"
                        onClick={() => setRestEnd(null)}
                      >
                        ×
                      </button>
                    </div>
                  )}
                  <label>
                    Repos
                    <select
                      value={restDurations[e.id] || restSecondsFor(e)}
                      onChange={(event) =>
                        setRestDurations((current) => ({
                          ...current,
                          [e.id]: Number(event.target.value),
                        }))
                      }
                    >
                      <option value="60">1 min</option>
                      <option value="90">1 min 30</option>
                      <option value="120">2 min</option>
                      <option value="150">2 min 30</option>
                      <option value="180">3 min</option>
                    </select>
                  </label>
                  <button
                    onClick={() => {
                      setReplaceExerciseId(e.id);
                      setExerciseSearch("");
                    }}
                  >
                    ↻ Remplacer
                  </button>
                  <button onClick={() => openExerciseEditor(e)}>✎ Modifier</button>
                  <button className="remove-control" onClick={() => removeExercise(e.id)}>Retirer</button>
                </div>
                <p className="alternative">Alternative : {e.alt || "—"}</p>
              </article>
            );
          })}
        </section>
        {replaceExerciseId && (
          <div
            className="modal-backdrop"
            role="presentation"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) {
                setReplaceExerciseId(null);
                setExerciseSearch("");
              }
            }}
          >
            <section
              className="replace-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="replace-title"
            >
              <div className="modal-heading">
                <div>
                  <small>CHANGER DE MACHINE OU D’EXERCICE</small>
                  <h2 id="replace-title">Remplacer l’exercice</h2>
                </div>
                <button
                  aria-label="Fermer"
                  onClick={() => {
                    setReplaceExerciseId(null);
                    setExerciseSearch("");
                  }}
                >
                  ×
                </button>
              </div>
              <div className="exercise-search modal-search">
                <span>⌕</span>
                <input
                  autoFocus
                  type="search"
                  value={exerciseSearch}
                  placeholder="Rechercher un exercice ou un muscle…"
                  onChange={(event) => setExerciseSearch(event.target.value)}
                />
              </div>
              <div className="exercise-results modal-results">
                {exerciseSearch ? (
                  freeResults.length ? (
                    freeResults.map((exercise) => (
                      <button
                        key={exercise.id}
                        onClick={() =>
                          replaceExercise(replaceExerciseId, exercise)
                        }
                      >
                        <span>
                          <strong>{technique(exercise)}</strong>
                          <small>{muscles(exercise)} · 4 × 8–12</small>
                        </span>
                        <b>→</b>
                      </button>
                    ))
                  ) : (
                    <p>Aucun exercice trouvé</p>
                  )
                ) : (
                  <p>Commence à saisir le nom d’un exercice ou d’un muscle.</p>
                )}
              </div>
              <button className="create-exercise-button" onClick={openExerciseCreator}>
                <span>＋</span><span><strong>Créer un nouvel exercice</strong><small>Il sera mémorisé dans ta recherche</small></span>
              </button>
            </section>
          </div>
        )}
        {editingExerciseId && exerciseEdit && (
          <div className="modal-backdrop" role="presentation">
            <section className="replace-modal edit-modal" role="dialog" aria-modal="true" aria-labelledby="edit-title">
              <div className="modal-heading">
                <div><small>{editingExerciseId === "__new__" ? "NOUVELLE MACHINE OU EXERCICE" : "PERSONNALISER CETTE SÉANCE"}</small><h2 id="edit-title">{editingExerciseId === "__new__" ? "Créer un exercice" : "Modifier l’exercice"}</h2></div>
                <button aria-label="Fermer" onClick={() => { setEditingExerciseId(null); setExerciseEdit(null); }}>×</button>
              </div>
              <div className="edit-fields">
                <label>Nom<input value={exerciseEdit.name} onChange={(e) => setExerciseEdit({ ...exerciseEdit, name: e.target.value })} /></label>
                <label>Muscle<input value={exerciseEdit.group} onChange={(e) => setExerciseEdit({ ...exerciseEdit, group: e.target.value })} /></label>
                <fieldset className="gym-multiselect"><legend>Salles disponibles</legend>{(["One Air", "Basic-Fit", "Toutes"] as Gym[]).map((gym) => <label key={gym}><input type="checkbox" checked={exerciseEdit.gyms.includes(gym)} onChange={() => setExerciseEdit({ ...exerciseEdit, gyms: exerciseEdit.gyms.includes(gym) ? exerciseEdit.gyms.filter((item) => item !== gym) : [...exerciseEdit.gyms, gym] })} />{gym}</label>)}</fieldset>
                <fieldset className="tag-picker"><legend>Tags prédéfinis</legend>{predefinedTagGroups.map((category) => <div className="tag-category" key={category.label}><span>{category.label}</span><div>{category.tags.map((tag) => { const selectedTag = exerciseEdit.tags.split(",").some((item) => normalizeExerciseName(item.trim()) === normalizeExerciseName(tag)); return <button type="button" aria-pressed={selectedTag} className={selectedTag ? "selected" : ""} key={tag} onClick={() => toggleExerciseTag(tag)}>{tag}</button>; })}</div></div>)}</fieldset>
                <label>Autres tags (facultatif)<input placeholder="Ex. prise neutre, convergente…" value={exerciseEdit.tags} onChange={(e) => setExerciseEdit({ ...exerciseEdit, tags: e.target.value })} /></label>
                {similarExercises.length > 0 && <aside className="duplicate-warning"><strong>Exercice similaire déjà présent</strong><p>Utilise l’exercice existant pour conserver un historique unique.</p>{similarExercises.map(({ exercise }) => <button type="button" key={exercise.id} onClick={() => useExistingExercise(exercise)}><span>{exercise.name}<small>{exercise.group} · {gymsFor(exercise).join(" · ")}</small></span><b>Utiliser</b></button>)}</aside>}
                <label>Séries<input type="number" inputMode="numeric" min="1" max="10" value={exerciseEdit.sets} onChange={(e) => setExerciseEdit({ ...exerciseEdit, sets: Number(e.target.value) })} /></label>
                <label>Objectif de répétitions<input value={exerciseEdit.target} onChange={(e) => setExerciseEdit({ ...exerciseEdit, target: e.target.value })} /></label>
                <label>Repos<select value={exerciseEdit.restSeconds} onChange={(e) => setExerciseEdit({ ...exerciseEdit, restSeconds: Number(e.target.value) })}><option value="60">1 min</option><option value="90">1 min 30</option><option value="120">2 min</option><option value="150">2 min 30</option><option value="180">3 min</option></select></label>
                <label>Lien vers la photo (facultatif)<input type="url" inputMode="url" placeholder="https://…" value={exerciseEdit.link} onChange={(e) => setExerciseEdit({ ...exerciseEdit, link: e.target.value })} /></label>
              </div>
              <button className="save-edit" onClick={saveExerciseEdit}>{editingExerciseId === "__new__" ? "Créer et ajouter à la séance" : "Enregistrer les modifications"}</button>
            </section>
          </div>
        )}
        {workoutHistoryOpen && (
          <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setWorkoutHistoryOpen(false); }}>
            <section className="replace-modal workout-history-modal" role="dialog" aria-modal="true" aria-labelledby="workout-history-title">
              <div className="modal-heading">
                <div><small>SANS QUITTER LA SÉANCE</small><h2 id="workout-history-title">Historique récent</h2></div>
                <button aria-label="Fermer" onClick={() => setWorkoutHistoryOpen(false)}>×</button>
              </div>
              {!logs.length ? <p className="history-modal-empty">Aucune séance terminée pour le moment.</p> : (
                <div className="workout-history-list">
                  {[...logs].reverse().slice(0, 10).map((log) => {
                    const session = sessions.find((item) => item.id === log.sessionId);
                    return <article key={log.id}>
                      <header><div><strong>{session?.name || (log.sessionId === "free" ? "Séance libre" : "Séance")}</strong><span>{new Intl.DateTimeFormat("fr-FR", { day: "numeric", month: "short", year: "numeric" }).format(new Date(log.date))} · {log.duration} min</span></div></header>
                      <div className="history-exercises">
                        {Object.entries(log.entries).filter(([, rows]) => rows.some((row) => row.weight || row.reps)).map(([exerciseId, rows]) => {
                          const baseId = exerciseId.split("@@")[0];
                          const gym = exerciseId.includes("@@") ? exerciseId.split("@@")[1] : "One Air";
                          const exercise = managedExerciseCatalog.find((item) => item.id === baseId);
                          const completed = rows.filter((row) => row.weight || row.reps);
                          return <div key={exerciseId}><b>{exercise?.name || "Exercice"} <em>· {gym}</em></b><span>{completed.map((row) => `${row.weight || "—"} kg × ${row.reps || "—"}`).join(" · ")}</span></div>;
                        })}
                      </div>
                    </article>;
                  })}
                </div>
              )}
            </section>
          </div>
        )}
        <div className="finish-bar">
          <div>
            <strong>{completedSets}</strong>
            <span>séries terminées</span>
          </div>
          <button onClick={finish}>Terminer la séance</button>
        </div>
        <nav className="bottom-nav workout-bottom-nav">
          {[["home", "Accueil"], ["program", "Programme"], ["exercises", "Exercices"], ["stats", "Progrès"], ["history", "Historique"], ["settings", "Réglages"]].map(([id, label]) => (
            <button onClick={() => { setTab(id); scrollTo(0, 0); }} key={id}><i>{icons[id]}</i><span>{label}</span></button>
          ))}
        </nav>
        {toast && <div className="toast">{toast}</div>}
      </main>
    );
  return (
    <main className={`app ${active ? "has-active-workout" : ""}`}>
      <header className="topbar">
        <div className="brand">
          <div className="brandmark">P</div>
          <div>
            <b>PERFORMANCE</b>
            <span>TRAIN SMARTER</span>
          </div>
        </div>
        <button
          className="theme-toggle"
          aria-label="Changer de thème"
          onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
        >
          {theme === "dark" ? "☀" : "☾"}
        </button>
      </header>
      <div className="content">
        {tab === "home" && (
          <>
            <section className="welcome">
              <p>ENTRAÎNEMENT</p>
              <h1>Prêt à progresser ?</h1>
              <span>Chaque série compte. Garde le rythme.</span>
            </section>
            <section className="metric-grid">
              <div className="metric lime">
                <small>CETTE SEMAINE</small>
                <strong>
                  {weekCount}
                  <em>séances</em>
                </strong>
                <span>
                  {weekCount >= 4
                    ? "Objectif atteint ✓"
                    : `${4 - weekCount} avant l’objectif`}
                </span>
              </div>
              <div className="metric">
                <small>VOLUME · 30 J</small>
                <strong>
                  {Math.round(monthVolume / 100) / 10}
                  <em>tonnes</em>
                </strong>
                <span>
                  {logs.length
                    ? "Basé sur tes saisies"
                    : "Commence ta 1re séance"}
                </span>
              </div>
            </section>
            <button className="free-workout-card" onClick={startFreeWorkout}>
              <span className="free-icon">＋</span>
              <span>
                <strong>Séance libre</strong>
                <small>Recherche et ajoute tes exercices à la volée</small>
              </span>
              <b>→</b>
            </button>
            <div className="section-title">
              <div>
                <small>MA SEMAINE</small>
                <h2>Programme actif</h2>
              </div>
              <span>
                {weekPlan.length} séance{weekPlan.length > 1 ? "s" : ""}
              </span>
            </div>
            <section className="session-list">
              {weekPlan.length ? (
                weekPlan.map((id, i) => {
                  const s = sessionForVariant(id);
                  return s ? (
                    <button
                      className="session-card"
                      key={`${id}-${i}`}
                      onClick={() => requestStart(s)}
                    >
                      <span
                        className="session-index"
                        style={{ color: s.color }}
                      >
                        {String(i + 1).padStart(2, "0")}
                      </span>
                      <div>
                        <h3>{s.name}</h3>
                        <p>{s.kicker}</p>
                        <small>{s.exercises.length} exercices</small>
                      </div>
                      <i style={{ background: s.color }}>→</i>
                    </button>
                  ) : null;
                })
              ) : (
                <button
                  className="empty-plan"
                  onClick={() => setTab("program")}
                >
                  <b>Créer ma semaine</b>
                  <span>
                    Ajoute librement des séances classiques ou ciblées
                  </span>
                </button>
              )}
            </section>
          </>
        )}
        {tab === "program" && (
          <>
            <section className="page-title">
              <small>
                BIBLIOTHÈQUE · {Object.values(coachingVariants).flat().length}{" "}
                SÉANCES
              </small>
              <h1>Choisis ta séance</h1>
              <p>
                Toutes les séances visent une silhouette plus dessinée avec
                quatre variantes réellement différentes.
              </p>
            </section>
            <section className="week-builder">
              <div className="week-title">
                <div>
                  <small>PROGRAMME ACTIF</small>
                  <h2>Ma semaine</h2>
                </div>
                <b>{weekPlan.length} séances</b>
              </div>
              {weekPlan.length ? (
                <div className="week-list">
                  {weekPlan.map((id, i) => {
                    const session = sessionForVariant(id);
                    return session ? (
                      <div className="week-item" key={`${id}-${i}`}>
                        <span>{i + 1}</span>
                        <strong>{session.name}</strong>
                        <div className="week-actions">
                          <button
                            disabled={i === 0}
                            onClick={() => moveWeek(i, -1)}
                          >
                            ↑
                          </button>
                          <button
                            disabled={i === weekPlan.length - 1}
                            onClick={() => moveWeek(i, 1)}
                          >
                            ↓
                          </button>
                          <button
                            aria-label="Supprimer"
                            onClick={() =>
                              setWeekPlan((p) => p.filter((_, n) => n !== i))
                            }
                          >
                            ×
                          </button>
                        </div>
                      </div>
                    ) : null;
                  })}
                </div>
              ) : (
                <p>
                  Ajoute les séances dans l’ordre souhaité. Tu peux choisir
                  plusieurs Pull ou plusieurs Jambes.
                </p>
              )}
            </section>
            <div className="goal-tabs family-tabs">
              <button
                className={programFamily === "classic" ? "active" : ""}
                onClick={() => {
                  setProgramFamily("classic");
                  setProgramCategory("push");
                  setSelectedVariant(coachingVariants.push[0].id);
                }}
              >
                Classiques
              </button>
              <button
                className={programFamily === "targeted" ? "active" : ""}
                onClick={() => {
                  setProgramFamily("targeted");
                  setProgramCategory("chest-biceps");
                  setSelectedVariant(coachingVariants["chest-biceps"][0].id);
                }}
              >
                Ciblées
              </button>
            </div>
            <div className="category-tabs">
              {visibleCategories.map((s) => (
                <button
                  key={s.id}
                  className={programCategory === s.id ? "active" : ""}
                  onClick={() => {
                    setProgramCategory(s.id);
                    setSelectedVariant(coachingVariants[s.id][0].id);
                  }}
                >
                  {s.name}
                </button>
              ))}
            </div>
            <p className="variant-format">4 séries par exercice</p>
            <section className="variant-grid">
              {visibleVariants.map((v) => (
                <button
                  key={v.id}
                  className={`variant-card ${selectedVariant === v.id ? "selected" : ""}`}
                  onClick={() => setSelectedVariant(v.id)}
                >
                  <strong>{v.name}</strong>
                  <span>{v.description}</span>
                </button>
              ))}
            </section>
            {(() => {
              const base = sessions.find((s) => s.id === programCategory)!,
                variant =
                  coachingVariants[programCategory].find(
                    (v) => v.id === selectedVariant,
                  ) ||
                  visibleVariants[0] ||
                  coachingVariants[programCategory][0],
                plan = buildVariant(base, variant),
                pool = visibleVariants.length
                  ? visibleVariants
                  : coachingVariants[programCategory],
                position = Math.max(
                  0,
                  pool.findIndex((v) => v.id === variant.id),
                );
              return (
                <section className="program-block variant-detail">
                  <div className="program-head">
                    <div
                      className="session-index"
                      style={{ color: plan.color }}
                    >
                      ✓
                    </div>
                    <div>
                      <h2>{plan.name}</h2>
                      <p>
                        {plan.exercises.length} exercices ·{" "}
                        {plan.exercises.length * 4} séries
                      </p>
                    </div>
                    <button onClick={() => requestStart(plan)}>Démarrer</button>
                  </div>
                  <button
                    className="add-week"
                    onClick={() => {
                      setWeekPlan((p) => [...p, variant.id]);
                      setToast("Séance ajoutée à la semaine ✓");
                    }}
                  >
                    + Ajouter à ma semaine
                  </button>
                  {plan.exercises.map((e, n) => (
                    <div className="program-row" key={e.id}>
                      <b>{n + 1}</b>
                      <div>
                        <strong>{technique(e)}</strong>
                        <span>
                          {muscles(e)} · {e.target} · {e.rest}
                        </span>
                        {oneAir && oneAirLabel(e) && (
                          <small className="machine-name">
                            {oneAirLabel(e)}
                          </small>
                        )}
                      </div>
                      {oneAir && e.link ? (
                        <a href={e.link} target="_blank" rel="noreferrer">
                          Photo ↗
                        </a>
                      ) : (
                        <i />
                      )}
                    </div>
                  ))}
                  <button
                    className="next-variant"
                    onClick={() =>
                      setSelectedVariant(pool[(position + 1) % pool.length].id)
                    }
                  >
                    ↻ Prochaine variante
                  </button>
                </section>
              );
            })()}
          </>
        )}
        {tab === "exercises" && (
          <>
            <section className="page-title exercise-page-title">
              <small>CATALOGUE PERSONNEL</small>
              <h1>Exercices</h1>
              <p>Ajoute tes machines, puis modifie ou supprime les exercices que tu n’utilises pas.</p>
            </section>
            <div className="exercise-search catalog-search">
              <span>⌕</span>
              <input type="search" value={catalogSearch} placeholder="Rechercher un exercice ou un muscle…" onChange={(event) => setCatalogSearch(event.target.value)} />
            </div>
            <div className="gym-filter catalog-gym-filter" aria-label="Filtrer les exercices par salle">
              {(["Toutes", "One Air", "Basic-Fit"] as const).map((gym) => <button key={gym} className={catalogGym === gym ? "active" : ""} onClick={() => setCatalogGym(gym)}>{gym}</button>)}
            </div>
            <section className="catalog-list">
              {managedExerciseCatalog
                .filter((exercise) => catalogGym === "Toutes" || gymsFor(exercise).some((gym) => gym === catalogGym || gym === "Toutes"))
                .filter((exercise) => !catalogSearch.trim() || normalizeSearch(`${exercise.name} ${exercise.group} ${technique(exercise)} ${muscles(exercise)} ${(exercise.tags || []).join(" ")} ${gymsFor(exercise).join(" ")}`).includes(normalizeSearch(catalogSearch.trim())))
                .map((exercise) => (
                  <article className="catalog-card" key={exercise.id}>
                    <div>
                      <strong>{exercise.name}</strong>
                      <span>{exercise.group} · {exercise.target} · repos {exercise.rest}</span>
                      <div className="exercise-tags">{gymsFor(exercise).map((gym) => <b key={gym}>{gym}</b>)}{(exercise.tags || []).map((tag) => <i key={tag}>#{tag}</i>)}</div>
                      {exercise.link && <a href={exercise.link} target="_blank" rel="noreferrer">Photo ↗</a>}
                    </div>
                    <div className="catalog-actions">
                      <button onClick={() => openCatalogEditor(exercise)}>Modifier</button>
                      <button className="danger" onClick={() => deleteCatalogExercise(exercise.id)}>Supprimer</button>
                    </div>
                  </article>
                ))}
            </section>
            <button className={`catalog-add sticky-catalog-add ${active ? "with-resume" : ""}`} onClick={openCatalogCreator}>＋ Ajouter un exercice</button>
            {editingExerciseId && exerciseEdit && (
              <div className="modal-backdrop" role="presentation">
                <section className="replace-modal edit-modal" role="dialog" aria-modal="true" aria-labelledby="catalog-edit-title">
                  <div className="modal-heading">
                    <div><small>CATALOGUE D’EXERCICES</small><h2 id="catalog-edit-title">{editingExerciseId === "__new__" ? "Ajouter un exercice" : "Modifier l’exercice"}</h2></div>
                    <button aria-label="Fermer" onClick={() => { setEditingExerciseId(null); setExerciseEdit(null); }}>×</button>
                  </div>
                  <div className="edit-fields">
                    <label>Nom<input autoFocus value={exerciseEdit.name} onChange={(e) => setExerciseEdit({ ...exerciseEdit, name: e.target.value })} /></label>
                    <label>Muscle<input value={exerciseEdit.group} onChange={(e) => setExerciseEdit({ ...exerciseEdit, group: e.target.value })} /></label>
                    <fieldset className="gym-multiselect"><legend>Salles disponibles</legend>{(["One Air", "Basic-Fit", "Toutes"] as Gym[]).map((gym) => <label key={gym}><input type="checkbox" checked={exerciseEdit.gyms.includes(gym)} onChange={() => setExerciseEdit({ ...exerciseEdit, gyms: exerciseEdit.gyms.includes(gym) ? exerciseEdit.gyms.filter((item) => item !== gym) : [...exerciseEdit.gyms, gym] })} />{gym}</label>)}</fieldset>
                    <fieldset className="tag-picker"><legend>Tags prédéfinis</legend>{predefinedTagGroups.map((category) => <div className="tag-category" key={category.label}><span>{category.label}</span><div>{category.tags.map((tag) => { const selectedTag = exerciseEdit.tags.split(",").some((item) => normalizeExerciseName(item.trim()) === normalizeExerciseName(tag)); return <button type="button" aria-pressed={selectedTag} className={selectedTag ? "selected" : ""} key={tag} onClick={() => toggleExerciseTag(tag)}>{tag}</button>; })}</div></div>)}</fieldset>
                    <label>Autres tags (facultatif)<input placeholder="Ex. prise neutre, convergente…" value={exerciseEdit.tags} onChange={(e) => setExerciseEdit({ ...exerciseEdit, tags: e.target.value })} /></label>
                    {similarExercises.length > 0 && <aside className="duplicate-warning"><strong>Exercice similaire déjà présent</strong><p>Utilise l’exercice existant pour conserver un historique unique.</p>{similarExercises.map(({ exercise }) => <button type="button" key={exercise.id} onClick={() => useExistingExercise(exercise)}><span>{exercise.name}<small>{exercise.group} · {gymsFor(exercise).join(" · ")}</small></span><b>Utiliser</b></button>)}</aside>}
                    <label>Séries<input type="number" inputMode="numeric" min="1" max="10" value={exerciseEdit.sets} onChange={(e) => setExerciseEdit({ ...exerciseEdit, sets: Number(e.target.value) })} /></label>
                    <label>Objectif de répétitions<input value={exerciseEdit.target} onChange={(e) => setExerciseEdit({ ...exerciseEdit, target: e.target.value })} /></label>
                    <label>Repos<select value={exerciseEdit.restSeconds} onChange={(e) => setExerciseEdit({ ...exerciseEdit, restSeconds: Number(e.target.value) })}><option value="60">1 min</option><option value="90">1 min 30</option><option value="120">2 min</option><option value="150">2 min 30</option><option value="180">3 min</option></select></label>
                    <label>Lien vers la photo (facultatif)<input type="url" inputMode="url" placeholder="https://…" value={exerciseEdit.link} onChange={(e) => setExerciseEdit({ ...exerciseEdit, link: e.target.value })} /></label>
                  </div>
                  <button className="save-edit" onClick={saveExerciseEdit}>{editingExerciseId === "__new__" ? "Ajouter au catalogue" : "Enregistrer les modifications"}</button>
                </section>
              </div>
            )}
          </>
        )}
        {tab === "stats" && (
          <>
            <section className="page-title">
              <small>28 DERNIERS JOURS</small>
              <h1>Mes progrès</h1>
              <p>Comprends ce que tu entraînes réellement.</p>
            </section>
            <section className="progress-summary">
              <article><strong>{recentLogs.length}</strong><span>Séances</span></article>
              <article><strong>{recentSetCount}</strong><span>Séries saisies</span></article>
              <article><strong>{averageDuration || "—"}</strong><span>{averageDuration ? "Min / séance" : "Durée moyenne"}</span></article>
            </section>
            {!recentLogs.length ? (
              <section className="progress-insight neutral">
                <small>BILAN</small>
                <h2>Pas encore assez de données</h2>
                <p>Termine une séance pour obtenir la répartition de tes programmes et des zones travaillées.</p>
              </section>
            ) : (
              <section className={`progress-insight ${neglectedAreas.length ? "warning" : "good"}`}>
                <small>BILAN DE RÉGULARITÉ</small>
                <h2>{neglectedAreas.length ? "Certaines zones sont absentes" : "Toutes les grandes zones sont présentes"}</h2>
                <p>
                  {neglectedAreas.length
                    ? `${neglectedAreas.join(", ")} : aucune série directe enregistrée sur cette période.`
                    : lightAreas.length
                      ? `${lightAreas.join(", ")} apparaissent moins souvent que le reste.`
                      : "La répartition de tes séries est homogène sur les quatre dernières semaines."}
                </p>
              </section>
            )}
            <section className="progress-panel">
              <header><div><small>RÉPARTITION</small><h2>Programmes réalisés</h2></div><span>{recentLogs.length} au total</span></header>
              {programCounts.length ? (
                <div className="progress-bars">
                  {programCounts.map(([name, count]) => (
                    <div className="progress-bar-row" key={name}>
                      <div><strong>{name}</strong><span>{count} séance{count > 1 ? "s" : ""}</span></div>
                      <i><b style={{ width: `${(count / maxProgramCount) * 100}%` }} /></i>
                    </div>
                  ))}
                </div>
              ) : <p className="progress-empty">Aucune séance sur cette période.</p>}
            </section>
            <section className="progress-panel">
              <header><div><small>ÉQUILIBRE</small><h2>Zones travaillées</h2></div><span>Séries directes</span></header>
              <div className="progress-bars area-bars">
                {progressAreas.map((area) => {
                  const count = areaCounts[area] || 0;
                  return (
                    <div className={`progress-bar-row ${count === 0 ? "missing" : ""}`} key={area}>
                      <div><strong>{area}</strong><span>{count || "Aucune"} {count ? "séries" : ""}</span></div>
                      <i><b style={{ width: `${(count / maxAreaCount) * 100}%` }} /></i>
                    </div>
                  );
                })}
              </div>
              <p className="progress-note">Ce graphique compte les séries enregistrées sur les muscles principalement ciblés. Les sollicitations secondaires ne sont pas additionnées.</p>
            </section>
            <div className="progress-detail-title"><small>DÉTAIL</small><h2>Progression par exercice</h2></div>
            <label className="select-label">
              EXERCICE
              <select
                value={selected}
                onChange={(e) => setSelected(e.target.value)}
              >
                {managedExerciseCatalog.map((e) => (
                  <option value={e.id} key={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            </label>
            <section className="chart-card">
              <div className="chart-head">
                <div>
                  <small>CHARGE MAXIMALE</small>
                  <h2>
                    {history.at(-1)?.max || 0} <em>kg</em>
                  </h2>
                </div>
                <span>{history.length} séances</span>
              </div>
              <Sparkline values={history.map((x) => x.max)} />
              <div className="chart-foot">
                <span>
                  Début
                  <br />
                  <b>{history[0]?.max || 0} kg</b>
                </span>
                <span>
                  Progression
                  <br />
                  <b>
                    {history.length > 1
                      ? `${history.at(-1)!.max - history[0].max >= 0 ? "+" : ""}${history.at(-1)!.max - history[0].max} kg`
                      : "—"}
                  </b>
                </span>
                <span>
                  Record
                  <br />
                  <b>{Math.max(0, ...history.map((x) => x.max))} kg</b>
                </span>
              </div>
            </section>
            <section className="record-card">
              <div>🏆</div>
              <div>
                <small>RECORD PERSONNEL</small>
                <h3>{currentEx.name}</h3>
                <p>{Math.max(0, ...history.map((x) => x.max))} kg</p>
              </div>
            </section>
          </>
        )}
        {tab === "history" && (
          <>
            <section className="page-title">
              <small>JOURNAL</small>
              <h1>Historique</h1>
              <p>
                {logs.length} séance{logs.length !== 1 ? "s" : ""} enregistrée
                {logs.length !== 1 ? "s" : ""}.
              </p>
            </section>
            {!logs.length ? (
              <div className="empty">
                <b>◷</b>
                <h2>Aucune séance</h2>
                <p>
                  Ta première séance apparaîtra ici avec son volume et sa durée.
                </p>
                <button onClick={() => setTab("home")}>
                  Choisir une séance
                </button>
              </div>
            ) : (
              <section className="history-list">
                {[...logs].reverse().map((l) => {
                  const s = sessions.find((x) => x.id === l.sessionId),
                    sets = Object.values(l.entries)
                      .flat()
                      .filter((x) => x.weight && x.reps).length;
                  return (
                    <article key={l.id}>
                      <div className="history-date">
                        <b>{new Date(l.date).getDate()}</b>
                        <span>
                          {new Intl.DateTimeFormat("fr-FR", {
                            month: "short",
                          }).format(new Date(l.date))}
                        </span>
                      </div>
                      <div>
                        <h3>{s?.name || "Séance"}</h3>
                        <p>
                          {new Intl.DateTimeFormat("fr-FR", {
                            day: "numeric",
                            month: "long",
                            year: "numeric",
                          }).format(new Date(l.date))}
                        </p>
                        <small>
                          {sets} séries · {l.duration} min ·{" "}
                          {Math.round(totalVolume(l) / 100) / 10} t
                        </small>
                      </div>
                      <button
                        onClick={() => {
                          if (confirm("Supprimer cette séance ?"))
                            setLogs((x) => x.filter((a) => a.id !== l.id));
                        }}
                      >
                        ×
                      </button>
                    </article>
                  );
                })}
              </section>
            )}
          </>
        )}
        {tab === "settings" && (
          <>
            <section className="page-title">
              <small>DONNÉES</small>
              <h1>Réglages</h1>
              <p>Tes données restent enregistrées sur cet appareil.</p>
            </section>
            <section className="settings-card">
              <h2>Sauvegarde</h2>
              <p>
                Exporte régulièrement ton historique pour pouvoir le restaurer
                sur un autre téléphone.
              </p>
              <div className="settings-actions">
                <button
                  onClick={() => {
                    const b = new Blob([JSON.stringify(logs, null, 2)], {
                        type: "application/json",
                      }),
                      a = document.createElement("a");
                    a.href = URL.createObjectURL(b);
                    a.download = `mes-performances-${new Date().toISOString().slice(0, 10)}.json`;
                    a.click();
                    setToast("Sauvegarde téléchargée");
                  }}
                >
                  ↓ Exporter mes données
                </button>
                <label>
                  ↑ Importer une sauvegarde
                  <input
                    type="file"
                    accept="application/json"
                    onChange={async (e) => {
                      const f = e.target.files?.[0];
                      if (!f) return;
                      try {
                        const d = JSON.parse(await f.text());
                        if (!Array.isArray(d)) throw Error();
                        setLogs(d);
                        setToast("Sauvegarde restaurée ✓");
                      } catch {
                        setToast("Fichier non reconnu");
                      }
                    }}
                  />
                </label>
              </div>
            </section>
            <section className="settings-card">
              <div className="inventory-head">
                <div>
                  <h2>Ma salle</h2>
                  <p>Inventaire utilisé pour construire les programmes.</p>
                </div>
                <b>{equipment.length}</b>
              </div>
              <div className="equipment-list">
                {equipment.map((item, i) => (
                  <div className="equipment-row" key={item.name}>
                    <span>{i + 1}</span>
                    <div>
                      <strong>{item.name}</strong>
                      <small>
                        {item.group} · {item.kind}
                      </small>
                    </div>
                    <i>✓</i>
                  </div>
                ))}
              </div>
            </section>
            <section className="settings-card">
              <h2>À propos</h2>
              <div className="about-row">
                <span>Stockage</span>
                <b>Local sur cet appareil</b>
              </div>
              <div className="about-row">
                <span>Programme</span>
                <b>4 + 1 optionnelle</b>
              </div>
              <div className="about-row">
                <span>Version</span>
                <b>1.1</b>
              </div>
            </section>
          </>
        )}
      </div>
      {pendingSession && (
        <div className="modal-backdrop" role="presentation">
          <section className="replace-modal start-session-modal" role="dialog" aria-modal="true" aria-labelledby="start-session-title">
            <div className="modal-heading">
              <div><small>PRÉPARATION</small><h2 id="start-session-title">{pendingSession.name}</h2></div>
              <button aria-label="Fermer" onClick={() => setPendingSession(null)}>×</button>
            </div>
            <div className="start-session-summary">
              <div><strong>{pendingSession.exercises.length || "Libre"}</strong><span>{pendingSession.exercises.length ? "exercices" : "ajout d’exercices à la volée"}</span></div>
              {pendingSession.exercises.length > 0 && <div><strong>{pendingSession.exercises.length * 4}</strong><span>séries prévues</span></div>}
            </div>
            <p className="start-session-copy">La séance et le chronomètre commenceront uniquement après confirmation.</p>
            <div className="start-session-actions">
              <button className="secondary-action" onClick={() => setPendingSession(null)}>Annuler</button>
              <button className="primary-action" onClick={confirmStart}>Démarrer la séance</button>
            </div>
          </section>
        </div>
      )}
      {active && tab !== "workout" && (
        <button className="resume-workout" onClick={() => { setTab("workout"); scrollTo(0, 0); }}>
          <span><small>SÉANCE EN COURS</small><strong>{active.name}</strong></span>
          <b>{formatTimer(Math.floor((now - started) / 1000))} · Reprendre</b>
        </button>
      )}
      <nav className="bottom-nav">
        {[
          ["home", "Accueil"],
          ["program", "Programme"],
          ["exercises", "Exercices"],
          ["stats", "Progrès"],
          ["history", "Historique"],
          ["settings", "Réglages"],
        ].map(([id, label]) => (
          <button
            className={tab === id ? "active" : ""}
            onClick={() => {
              setTab(id);
              scrollTo(0, 0);
            }}
            key={id}
          >
            <i>{icons[id]}</i>
            <span>{label}</span>
          </button>
        ))}
      </nav>
      {toast && <div className="toast">{toast}</div>}
    </main>
  );
}
