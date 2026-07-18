<template>
  <main>
    <h1>Architecture Patterns Demo</h1>
    <p>
      This page demonstrates the full type-safety chain:<br />
      <code
        >C# record → Swagger → openapi.json → schemas.ts → Vue component</code
      >
    </p>

    <section class="toolbar">
      <button :disabled="store.loading" @click="createDemo">
        + Create demo request
      </button>
      <span v-if="store.loading" class="hint">Loading…</span>
      <span v-if="store.error" class="hint hint--error">{{ store.error }}</span>
    </section>

    <p
      v-if="!store.loading && store.pending.length === 0 && !store.current"
      class="hint"
    >
      No pending requests. Click the button above to create one.
    </p>

    <GroupJoinRequestCard
      v-if="store.current"
      :request="store.current"
      @accept="(id, body) => store.accept(id, body)"
    />

    <ul v-if="store.pending.length > 0" class="list">
      <li
        v-for="req in store.pending"
        :key="req.id"
        class="list__item"
        :class="{ 'list__item--active': store.current?.id === req.id }"
        @click="store.load(req.id)"
      >
        <span class="list__id">{{ req.id.slice(0, 8) }}…</span>
        <span class="list__meta">Student {{ req.studentId.slice(0, 8) }}</span>
        <span class="list__date">{{ formatDate(req.requestedAt) }}</span>
      </li>
    </ul>
  </main>
</template>

<script setup lang="ts">
import { onMounted } from "vue";
import { useGroupJoinRequestsStore } from "@/stores/groupJoinRequestsStore";
import GroupJoinRequestCard from "@/components/GroupJoinRequestCard.vue";

const store = useGroupJoinRequestsStore();

onMounted(() => store.loadPending());

function createDemo() {
  store.submit({
    studentId: crypto.randomUUID(),
    groupId: crypto.randomUUID(),
  });
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}
</script>

<style>
* {
  box-sizing: border-box;
}
body {
  font-family: system-ui, sans-serif;
  padding: 2rem;
  max-width: 640px;
  margin: auto;
}
code {
  background: #f3f4f6;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 0.85em;
}
</style>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin: 1.5rem 0 1rem;
}
.toolbar button {
  padding: 6px 14px;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
}
.toolbar button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.hint {
  font-size: 0.85rem;
  color: #888;
}
.hint--error {
  color: #c0392b;
}

.list {
  list-style: none;
  padding: 0;
  margin-top: 1.5rem;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  overflow: hidden;
}
.list__item {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 0.5rem;
  align-items: center;
  padding: 0.6rem 1rem;
  cursor: pointer;
  border-bottom: 1px solid #f3f4f6;
  font-size: 0.85rem;
}
.list__item:last-child {
  border-bottom: none;
}
.list__item:hover {
  background: #f9fafb;
}
.list__item--active {
  background: #eff6ff;
}
.list__id {
  font-family: monospace;
  color: #374151;
}
.list__meta {
  color: #6b7280;
  font-size: 0.8rem;
}
.list__date {
  color: #9ca3af;
  font-size: 0.75rem;
  white-space: nowrap;
}
</style>
