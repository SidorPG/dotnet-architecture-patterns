<template>
  <main>
    <h1>Architecture Patterns Demo</h1>
    <p>
      This page demonstrates the full type-safety chain:<br />
      <code
        >C# record → Swagger → openapi.json → schemas.ts → Vue component</code
      >
    </p>
    <GroupJoinRequestCard request-id="00000000-0000-0000-0000-000000000001" />
  </main>
</template>

<script setup lang="ts">
import GroupJoinRequestCard from "@/components/GroupJoinRequestCard.vue";
</script>

<style>
* {
  box-sizing: border-box;
}
body {
  font-family: system-ui, sans-serif;
  padding: 2rem;
  max-width: 600px;
  margin: auto;
}
code {
  background: #f3f4f6;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 0.85em;
}
</style>
