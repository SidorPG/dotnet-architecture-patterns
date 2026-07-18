<template>
  <div class="card">
    <header class="card__header">
      <span class="card__status" :class="`card__status--${statusClass}`">
        {{ request.status }}
      </span>
      <span class="card__id">{{ request.id }}</span>
    </header>

    <dl class="card__body">
      <dt>Student</dt>
      <dd>{{ request.studentId }}</dd>
      <dt>Group</dt>
      <dd>{{ request.groupId }}</dd>
      <dt>Requested</dt>
      <dd>{{ formatDate(request.requestedAt) }}</dd>
      <template v-if="request.agreedPrice != null">
        <dt>Agreed price</dt>
        <dd>{{ request.agreedPrice }} {{ request.agreedCurrency }}</dd>
      </template>
    </dl>

    <form
      v-if="request.status === 'PendingApproval'"
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
      <button type="submit">Accept</button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed } from "vue";
import type {
  GroupJoinRequestDto,
  JoinRequestStatus,
  AcceptBody,
} from "@/services/groupJoinRequestsService";

const props = defineProps<{ request: GroupJoinRequestDto }>();
const emit = defineEmits<{ accept: [id: string, body: AcceptBody] }>();

const form = reactive({ agreedPrice: 0, agreedCurrency: "EUR" });

function handleAccept() {
  emit("accept", props.request.id, {
    agreedPrice: form.agreedPrice,
    agreedCurrency: form.agreedCurrency,
  });
}

const statusClass = computed<string>(() => {
  const map: Record<JoinRequestStatus, string> = {
    PendingApproval: "pending",
    PendingPayment: "payment",
    Confirmed: "confirmed",
    Rejected: "rejected",
    Cancelled: "cancelled",
  };
  return map[props.request.status];
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
  font-family: sans-serif;
  margin-bottom: 1rem;
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
</style>
