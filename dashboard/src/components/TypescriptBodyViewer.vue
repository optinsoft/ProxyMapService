<script setup lang="ts">
import type { HttpTypescriptBodyEntry } from '@/types/log';

defineProps<{
  body: HttpTypescriptBodyEntry;
}>();
</script>

<template>
  <pre class="text-viewer">
{{ body.content }}
  </pre>
</template>

<style scoped>
.text-viewer {
  overflow: auto;
  padding: 12px;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>