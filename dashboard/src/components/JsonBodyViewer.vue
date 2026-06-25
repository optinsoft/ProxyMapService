<script setup lang="ts">
import { computed } from 'vue';
import type { HttpJsonBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpJsonBodyEntry;
}>();

const formattedJson = computed(() => {
  try {
    return JSON.stringify(
      JSON.parse(props.body.content),
      null,
      2,
    );
  } catch {
    return props.body.content;
  }
});
</script>

<template>
  <pre class="json-viewer">{{ formattedJson }}</pre>
</template>

<style scoped>
.json-viewer {
  overflow: auto;
  padding: 12px;
  font-family: monospace;
  white-space: pre-wrap;
}
</style>