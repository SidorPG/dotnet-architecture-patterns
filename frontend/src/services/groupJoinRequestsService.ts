import axios from "axios";
import type { components } from "@/api/schemas";

export type GroupJoinRequestDto = components["schemas"]["GroupJoinRequestDto"];
export type JoinRequestStatus = components["schemas"]["JoinRequestStatus"];
export type SubmitBody = components["schemas"]["SubmitBody"];
export type SubmitResult = components["schemas"]["SubmitResult"];
export type AcceptBody = components["schemas"]["AcceptBody"];

const http = axios.create({ baseURL: "/api/v1" });

export const groupJoinRequestsService = {
  async getPending(): Promise<GroupJoinRequestDto[]> {
    const { data } = await http.get<GroupJoinRequestDto[]>(
      "/group-join-requests",
    );
    return data;
  },

  async getById(id: string): Promise<GroupJoinRequestDto> {
    const { data } = await http.get<GroupJoinRequestDto>(
      `/group-join-requests/${id}`,
    );
    return data;
  },

  async submit(body: SubmitBody): Promise<SubmitResult> {
    const { data } = await http.post<SubmitResult>(
      "/group-join-requests",
      body,
    );
    return data;
  },

  async accept(id: string, body: AcceptBody): Promise<void> {
    await http.post(`/group-join-requests/${id}/accept`, body);
  },
};
