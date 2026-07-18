<template>
  <div class="card">
    <div v-if="store.loading" class="card__state">Loading…</div>

    <div v-else-if="store.error" class="card__state card__state--error">
      {{ store.error }}
    </div>

    <template v-else-if="store.current">
      <header class="card__header">
        <span class="card__status" :class="`card__status--${statusClass}`">
          {{ store.current.status }}
        </span>
        <span class="card__id">{{ store.current.id }}</span>
      </header>

      <dl class="card__body">
        <dt>Student</dt>
        <dd>{{ store.current.studentId }}</dd>
        <dt>Group</dt>
        <dd>{{ store.current.groupId }}</dd>
        <dt>Requested</dt>
        <dd>{{ formatDate(store.current.requestedAt) }}</dd>
        <template v-if="store.current.agreedPrice != null">
          <dt>Agreed price</dt>
          <dd>
            {{ store.current.agreedPrice }} {{ store.current.agreedCurrency }}
          </dd>
        </template>
      </dl>

      <form
        v-if="store.current.status === 'PendingApproval'"
        class="card__accept"
        @submit.prevent="handleAccept"
      >
        <input
          v-model.number="form.agreedPrice"
          type="number"
          step="0.01"
          min="0"
          placeholder="Price"
          required
        />
        <input
          v-model="form.agreedCurrency"
          type="text"
          maxlength="3"
          placeholder="EUR"
          required
        />
        <button type="submit" :disabled="store.loading">Accept</button>
      </form>
    </template>

    <div v-else class="card__state">No request loaded.</div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, onMounted } from "vue";
import { useGroupJoinRequestsStore } from "@/stores/groupJoinRequestsStore";
import type { JoinRequestStatus } from "@/services/groupJoinRequestsService";

const props = defineProps<{ requestId: string }>();

const store = useGroupJoinRequestsStore();

const form = reactive({ agreedPrice: 0, agreedCurrency: "EUR" });

onMounted(() => store.load(props.requestId));

async function handleAccept() {
  await store.accept(props.requestId, {
    agreedPrice: form.agreedPrice,
    agreedCurrency: form.agreedCurrency,
  });
}

// Maps JoinRequestStatus → CSS modifier for the status badge.
const statusClass = computed<string>(() => {
  const map: Record<JoinRequestStatus, string> = {
    PendingApproval: "pending",
    PendingPayment: "payment",
    Confirmed: "confirmed",
    Rejected: "rejected",
    Cancelled: "cancelled",
  };
  return store.current ? map[store.current.status] : "";
});

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}
</script>

<style scoped>
.card {
  border: 1px solid var(--color-border, #ddd);
  border-radius: 8px;
  padding: 1rem;
  max-width: 480px;
  font-family: sans-serif;
}
.card__state {
  color: #888;
}
.card__state--error {
  color: #c0392b;
}

.card__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
}
.card__id {
  font-size: 0.7rem;
  color: #888;
  font-family: monospace;
}

.card__status {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 12px;
}
.card__status--pending {
  background: #fef3c7;
  color: #92400e;
}
.card__status--payment {
  background: #dbeafe;
  color: #1e40af;
}
.card__status--confirmed {
  background: #d1fae5;
  color: #065f46;
}
.card__status--rejected,
.card__status--cancelled {
  background: #fee2e2;
  color: #991b1b;
}

.card__body {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 4px 12px;
}
.card__body dt {
  font-weight: 500;
  color: #555;
}
.card__body dd {
  margin: 0;
}

.card__accept {
  display: flex;
  gap: 8px;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #eee;
}
.card__accept input {
  flex: 1;
  padding: 6px 8px;
  border: 1px solid #ccc;
  border-radius: 4px;
}
.card__accept button {
  padding: 6px 16px;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}
.card__accept button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
