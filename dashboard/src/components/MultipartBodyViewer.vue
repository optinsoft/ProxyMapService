<script setup lang="ts">
import JsonBodyViewer from './JsonBodyViewer.vue';
import XmlBodyViewer from './XmlBodyViewer.vue';
import HtmlBodyViewer from './HtmlBodyViewer.vue';
import TextBodyViewer from './TextBodyViewer.vue';
import ImageBodyViewer from './ImageBodyViewer.vue';
import BinaryBodyViewer from './BinaryBodyViewer.vue';
import FormUrlEncodedBodyViewer from './FormUrlEncodedBodyViewer.vue';
import JavascriptBodyViewer from './JavascriptBodyViewer.vue';
import TypescriptBodyViewer from './TypescriptBodyViewer.vue';

import { type HttpMultipartBodyEntry, HttpContentKind } from '@/types/log';

defineProps<{
  body: HttpMultipartBodyEntry;
}>();
</script>

<template>
  <div class="multipart-viewer">
    <div
      v-for="(part, index) in body.parts"
      :key="index"
      class="part"
    >
      <div class="part-header">
        <div v-if="part.name" class="part-header-row">
          <span class="part-header-key">Name:</span>
          <span class="part-header-val">{{ part.name }}</span>
        </div>

        <div v-if="part.fileName" class="part-header-row">
          <span class="part-header-key">File:</span> 
          <span class="part-header-val">{{ part.fileName }}</span>
        </div>

        <div v-if="part.contentType" class="part-header-row">
          <span class="part-header-key">Content-Type:</span>
          <span class="part-header-val">{{ part.contentType }}</span>
        </div>

        <div class="part-header-row">
          <span class="part-header-key">Length:</span>
          <span class="part-header-val">{{ part.length }} bytes</span>
        </div>
      </div>

      <div class="part-content">
        <JsonBodyViewer
          v-if="part.contentKind === HttpContentKind.Json"
          :body="part"
        />

        <XmlBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Xml"
          :body="part"
        />

        <HtmlBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Html"
          :body="part"
        />

        <TextBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Text"
          :body="part"
        />

        <JavascriptBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Javascript"
          :body="part"
        />

        <TypescriptBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Typescript"
          :body="part"
        />

        <FormUrlEncodedBodyViewer
          v-else-if="part.contentKind === HttpContentKind.FormUrlEncoded"
          :body="part"
        />

        <MultipartBodyViewer
          v-else-if="part.contentKind === HttpContentKind.MultipartFormData"
          :body="part"
        />        

        <ImageBodyViewer
          v-else-if="part.contentKind === HttpContentKind.Image"
          :body="part"
        />

        <BinaryBodyViewer
           v-else-if="part.contentKind === HttpContentKind.Binary"
          :body="part"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.multipart-viewer {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.part {
  border: 1px solid #3c3c3c;
  border-radius: 4px;
}

.part-header {
  padding: 8px 12px;
  background: #252526;
  border-bottom: 1px solid #3c3c3c;
  font-size: 12px;
}

.part-header-row { display: flex; padding: 3px 0; border-bottom: 1px solid #252526; gap: 6px; }
.part-header-row:last-child { border-bottom: none; }
.part-header-key { color: #569cd6; font-family: monospace; font-weight: 600; white-space: nowrap; }
.part-header-val { color: #ce9178; font-family: monospace; word-break: break-all; overflow: hidden; text-overflow: ellipsis; }

.part-content {
  padding: 8px;
}
</style>