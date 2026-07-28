/**
 * DocumentAnnotations — annotation overlay for the DocumentViewer.
 * Supports highlight, comment, and marker annotations on invoice documents.
 *
 * Usage:
 *   <DocumentAnnotations
 *     documentId={invoice.id}
 *     annotations={annotations}
 *     onAddAnnotation={(a) => saveAnnotation(a)}
 *   />
 */

"use client";

import { useState, useCallback } from "react";
import { cn } from "@/lib/utils";

// ─── Types ───────────────────────────────────────────────────

export interface Annotation {
  id: string;
  documentId: string;
  type: "highlight" | "comment" | "marker";
  pageNumber?: number;
  /** Relative x position (0-100) */
  x: number;
  /** Relative y position (0-100) */
  y: number;
  /** Width as percentage (0-100) */
  width?: number;
  /** Height as percentage (0-100) */
  height?: number;
  /** Comment text (for comment type) */
  text?: string;
  /** Author display name */
  author: string;
  /** UTC ISO timestamp */
  createdAt: string;
  /** Color hex */
  color?: string;
  resolved?: boolean;
}

interface DocumentAnnotationsProps {
  documentId: string;
  annotations: Annotation[];
  onAddAnnotation: (annotation: Omit<Annotation, "id" | "createdAt">) => void;
  onResolveAnnotation?: (id: string) => void;
  onDeleteAnnotation?: (id: string) => void;
  readOnly?: boolean;
}

// ─── Color Palette ───────────────────────────────────────────

const ANNOTATION_COLORS = [
  { label: "Yellow", value: "#FEF08A" },
  { label: "Green", value: "#BBF7D0" },
  { label: "Blue", value: "#BFDBFE" },
  { label: "Pink", value: "#FBCFE8" },
  { label: "Orange", value: "#FED7AA" },
];

// ─── Component ───────────────────────────────────────────────

export function DocumentAnnotations({
  annotations,
  onAddAnnotation,
  onResolveAnnotation,
  onDeleteAnnotation,
  readOnly = false,
}: DocumentAnnotationsProps) {
  const [mode, setMode] = useState<"none" | "highlight" | "comment">("none");
  const [selectedColor, setSelectedColor] = useState(ANNOTATION_COLORS[0].value);
  const [activeAnnotation, setActiveAnnotation] = useState<string | null>(null);
  const [commentText, setCommentText] = useState("");

  const handleCanvasClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (mode === "none" || readOnly) return;

      const rect = e.currentTarget.getBoundingClientRect();
      const x = ((e.clientX - rect.left) / rect.width) * 100;
      const y = ((e.clientY - rect.top) / rect.height) * 100;

      if (mode === "highlight") {
        onAddAnnotation({
          documentId: "",
          type: "highlight",
          x,
          y,
          width: 15,
          height: 3,
          color: selectedColor,
          author: "User",
        });
        setMode("none");
      } else if (mode === "comment") {
        const id = `temp-${Date.now()}`;
        setActiveAnnotation(id);
        onAddAnnotation({
          documentId: "",
          type: "comment",
          x,
          y,
          text: "",
          color: selectedColor,
          author: "User",
        });
      }
    },
    [mode, readOnly, selectedColor, onAddAnnotation]
  );

  const handleAddComment = useCallback(
    (annotationId: string) => {
      if (!commentText.trim()) return;        onAddAnnotation({
        documentId: "",
        type: "comment",
        x: 0,
        y: 0,
        text: commentText,
        color: selectedColor,
        author: "User",
      });
      setCommentText("");
      setActiveAnnotation(null);
    },
    [commentText, selectedColor, onAddAnnotation]
  );

  return (
    <div className="space-y-3">
      {/* Toolbar */}
      {!readOnly && (
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs font-medium text-gray-500 uppercase">Annotate:</span>
          <button
            onClick={() => setMode(mode === "highlight" ? "none" : "highlight")}
            className={cn(
              "px-2.5 py-1.5 rounded text-xs font-medium transition-colors border",
              mode === "highlight"
                ? "bg-yellow-100 border-yellow-300 text-yellow-800"
                : "bg-white border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            🖍 Highlight
          </button>
          <button
            onClick={() => setMode(mode === "comment" ? "none" : "comment")}
            className={cn(
              "px-2.5 py-1.5 rounded text-xs font-medium transition-colors border",
              mode === "comment"
                ? "bg-blue-100 border-blue-300 text-blue-800"
                : "bg-white border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            💬 Comment
          </button>
          {mode !== "none" && (
            <button
              onClick={() => setMode("none")}
              className="text-xs text-gray-400 hover:text-gray-600 ml-1"
            >
              Cancel
            </button>
          )}
          {/* Color picker */}
          <div className="flex items-center gap-1 ml-2 border-l border-gray-200 pl-2">
            {ANNOTATION_COLORS.map((c) => (
              <button
                key={c.value}
                onClick={() => setSelectedColor(c.value)}
                className={cn(
                  "w-4 h-4 rounded-full border transition-transform",
                  selectedColor === c.value ? "scale-125 ring-2 ring-offset-1 ring-gray-400" : ""
                )}
                style={{ backgroundColor: c.value }}
                title={c.label}
              />
            ))}
          </div>
        </div>
      )}

      {/* Annotation overlay */}
      <div className="relative">
        <div
          className={cn(
            "absolute inset-0 z-10",
            mode !== "none" ? "cursor-crosshair" : "pointer-events-none"
          )}
          onClick={handleCanvasClick}
        />

        {/* Render annotations */}
        {annotations.map((ann) => (
          <div key={ann.id}>
            {ann.type === "highlight" && (
              <div
                className="absolute rounded-sm pointer-events-none opacity-60"
                style={{
                  left: `${ann.x}%`,
                  top: `${ann.y}%`,
                  width: `${ann.width || 15}%`,
                  height: `${ann.height || 3}%`,
                  backgroundColor: ann.color || "#FEF08A",
                }}
              />
            )}
            {ann.type === "comment" && (
              <button
                onClick={() => setActiveAnnotation(activeAnnotation === ann.id ? null : ann.id)}
                className={cn(
                  "absolute w-6 h-6 -translate-x-1/2 -translate-y-1/2 rounded-full flex items-center justify-center text-xs font-bold transition-transform hover:scale-110 z-20",
                  activeAnnotation === ann.id ? "scale-125 ring-2 ring-offset-1 ring-blue-400" : ""
                )}
                style={{
                  left: `${ann.x}%`,
                  top: `${ann.y}%`,
                  backgroundColor: ann.color || "#BFDBFE",
                }}
              >
                💬
              </button>
            )}
            {ann.type === "marker" && (
              <div
                className="absolute w-3 h-3 -translate-x-1/2 -translate-y-1/2 rounded-full pointer-events-none"
                style={{
                  left: `${ann.x}%`,
                  top: `${ann.y}%`,
                  backgroundColor: ann.color || "#EF4444",
                }}
              />
            )}
          </div>
        ))}

        {/* Comment popup */}
        {activeAnnotation && !readOnly && (
          <div className="absolute z-30 bg-white rounded-lg shadow-xl border border-gray-200 p-3 w-64"
            style={{
              left: "50%",
              top: "50%",
              transform: "translate(-50%, -50%)",
            }}
          >
            <textarea
              value={commentText}
              onChange={(e) => setCommentText(e.target.value)}
              placeholder="Type your comment..."
              className="w-full text-sm border border-gray-200 rounded-md p-2 min-h-[60px] resize-none focus:outline-none focus:ring-1 focus:ring-primary"
              autoFocus
            />
            <div className="flex justify-end gap-2 mt-2">
              <button
                onClick={() => { setActiveAnnotation(null); setCommentText(""); }}
                className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1"
              >
                Cancel
              </button>
              <button
                onClick={() => handleAddComment(activeAnnotation)}
                className="text-xs bg-primary text-white rounded px-3 py-1 hover:bg-primary-600"
                disabled={!commentText.trim()}
              >
                Add Comment
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Annotation list */}
      {annotations.length > 0 && (
        <div className="space-y-1.5 border-t border-gray-100 pt-3">
          <p className="text-xs font-medium text-gray-500 uppercase">
            Annotations ({annotations.length})
          </p>
          <div className="max-h-32 overflow-y-auto space-y-1">
            {annotations.map((ann) => (
              <div
                key={ann.id}
                className={cn(
                  "flex items-start gap-2 p-2 rounded text-sm",
                  ann.resolved ? "opacity-50" : "bg-gray-50"
                )}
              >
                <span className="text-xs mt-0.5">
                  {ann.type === "highlight" ? "🖍" : ann.type === "comment" ? "💬" : "📍"}
                </span>
                <div className="flex-1 min-w-0">
                  <p className="text-xs text-gray-700 truncate">
                    {ann.text || `${ann.type} at (${Math.round(ann.x)}%, ${Math.round(ann.y)}%)`}
                  </p>
                  <p className="text-xs text-gray-400">
                    {ann.author} · {new Date(ann.createdAt).toLocaleDateString()}
                    {ann.resolved && " · Resolved"}
                  </p>
                </div>
                <div className="flex gap-1 shrink-0">
                  {!ann.resolved && onResolveAnnotation && (
                    <button
                      onClick={() => onResolveAnnotation(ann.id)}
                      className="text-xs text-accent hover:underline"
                      title="Resolve"
                    >
                      ✓
                    </button>
                  )}
                  {onDeleteAnnotation && (
                    <button
                      onClick={() => onDeleteAnnotation(ann.id)}
                      className="text-xs text-red-400 hover:text-red-600"
                      title="Delete"
                    >
                      ✕
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Mode hint */}
      {mode !== "none" && (
        <p className="text-xs text-gray-400 italic">
          {mode === "highlight"
            ? "Click on the document to add a highlight"
            : "Click on the document to place a comment pin"}
        </p>
      )}
    </div>
  );
}
