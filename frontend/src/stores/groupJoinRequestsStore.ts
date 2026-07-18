import { defineStore } from "pinia";
import { ref } from "vue";
import {
  groupJoinRequestsService,
  type GroupJoinRequestDto,
  type AcceptBody,
  type SubmitBody,
} from "@/services/groupJoinRequestsService";

export const useGroupJoinRequestsStore = defineStore(
  "groupJoinRequests",
  () => {
    const pending = ref<GroupJoinRequestDto[]>([]);
    const current = ref<GroupJoinRequestDto | null>(null);
    const loading = ref(false);
    const error = ref<string | null>(null);

    async function loadPending() {
      loading.value = true;
      error.value = null;
      try {
        pending.value = await groupJoinRequestsService.getPending();
      } catch (e: unknown) {
        error.value = e instanceof Error ? e.message : "Unknown error";
      } finally {
        loading.value = false;
      }
    }

    async function load(id: string) {
      loading.value = true;
      error.value = null;
      try {
        current.value = await groupJoinRequestsService.getById(id);
      } catch (e: unknown) {
        error.value = e instanceof Error ? e.message : "Unknown error";
      } finally {
        loading.value = false;
      }
    }

    async function submit(body: SubmitBody) {
      loading.value = true;
      error.value = null;
      try {
        const result = await groupJoinRequestsService.submit(body);
        await load(result.id);
        await loadPending();
      } catch (e: unknown) {
        error.value = e instanceof Error ? e.message : "Unknown error";
      } finally {
        loading.value = false;
      }
    }

    async function accept(id: string, body: AcceptBody) {
      loading.value = true;
      error.value = null;
      try {
        await groupJoinRequestsService.accept(id, body);
        await load(id);
        await loadPending();
      } catch (e: unknown) {
        error.value = e instanceof Error ? e.message : "Unknown error";
      } finally {
        loading.value = false;
      }
    }

    return {
      pending,
      current,
      loading,
      error,
      loadPending,
      load,
      submit,
      accept,
    };
  },
);
