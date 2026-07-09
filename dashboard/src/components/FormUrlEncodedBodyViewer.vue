<script setup lang="ts">
import { computed } from 'vue';
import type { HttpFormUrlEncodedBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpFormUrlEncodedBodyEntry;
}>();

const entries = computed(() => {
  const params = new URLSearchParams(props.body.content);

  return [...params.entries()].map(([key, value]) => ({
    key,
    value,
  }));
});
</script>

<template>
  <table class="form-table">
    <thead>
      <tr>
        <th>Key</th>
        <th>Value</th>
      </tr>
    </thead>

    <tbody>
      <tr
        v-for="entry in entries"
        :key="`${entry.key}:${entry.value}`"
      >
        <td class="key-column">{{ entry.key }}</td>
        <td class="value-column">{{ entry.value }}</td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.form-table {
  width: 100%;
  border-collapse: collapse;
  font-family: monospace;
}

.form-table th,
.form-table td {
  border: 1px solid #444;
  padding: 6px 8px;
  text-align: left;
  vertical-align: top;
}

.form-table th {
  background: #2d2d2d;
}

.form-table td {
  word-break: break-all;
}

.key-column {
  width: 150px;
  font-weight: bold;
  vertical-align: top;
}

.value-column {
  word-break: break-all;
  white-space: pre-wrap;
  vertical-align: top;
}
</style>