import axios from "axios";
import type { components } from "@/api/schemas";

// Types derived directly from the generated OpenAPI schema.
// If the C# DTO changes, `npm run openapi:generate` updates these automatically —
// any mismatched call-site becomes a TypeScript compile error.
export type GroupJoinRequestDto = components["schemas"]["GroupJoinRequestDto"];
export type JoinRequestStatus = components["schemas"]["JoinRequestStatus"];
export type AcceptBody = components["schemas"]["AcceptBody"];

const http = axios.create({ baseURL: "/api/v1" });

export const groupJoinRequestsService = {
  async getById(id: string): Promise<GroupJoinRequestDto> {
    const { data } = await http.get<GroupJoinRequestDto>(
      `/group-join-requests/${id}`,
    );
    return data;
  },

  async accept(id: string, body: AcceptBody): Promise<void> {
    await http.post(`/group-join-requests/${id}/accept`, body);
  },
};
