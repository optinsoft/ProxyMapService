<script setup lang="ts">
import type { HttpTextBodyEntry } from '@/types/log';

defineProps<{
  body: HttpTextBodyEntry;
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