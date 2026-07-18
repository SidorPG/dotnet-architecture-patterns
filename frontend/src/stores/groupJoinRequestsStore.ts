import { defineStore } from "pinia";
import { ref } from "vue";
import {
  groupJoinRequestsService,
  type GroupJoinRequestDto,
  type AcceptBody,
} from "@/services/groupJoinRequestsService";

// One store per feature — acts as the ViewModel between components and the HTTP service.
// Components never call the service directly.
export const useGroupJoinRequestsStore = defineStore(
  "groupJoinRequests",
  () => {
    const current = ref<GroupJoinRequestDto | null>(null);
    const loading = ref(false);
    const error = ref<string | null>(null);

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

    async function accept(id: string, body: AcceptBody) {
      loading.value = true;
      error.value = null;
      try {
        await groupJoinRequestsService.accept(id, body);
        await load(id); // refresh after state change
      } catch (e: unknown) {
        error.value = e instanceof Error ? e.message : "Unknown error";
      } finally {
        loading.value = false;
      }
    }

    return { current, loading, error, load, accept };
  },
);
