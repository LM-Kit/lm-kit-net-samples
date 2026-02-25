# Email Triage & Response Agent

A demo showcasing **supervisor-based multi-agent orchestration** for automated email processing. A supervisor agent coordinates three specialist workers to classify incoming emails, extract key information, and draft professional responses, all running locally on your hardware.

## Features

- **SupervisorOrchestrator**: Supervisor delegates to specialist workers in sequence
- **Email Classification**: Category, urgency, sentiment, and escalation detection
- **Information Extraction**: Sender intent, questions, deadlines, reference numbers
- **Response Drafting**: Context-aware, tone-matched professional email responses
- **Streaming Output**: Real-time display of each agent's work
- **Built-In Sample Email**: Try instantly with a realistic customer scenario
- **Multi-Line Input**: Paste full emails directly into the console
- **Privacy-First**: Sensitive email data never leaves your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (6-18 GB depending on model choice)

## How It Works

The supervisor orchestrator coordinates three specialist agents:

1. **Classifier**: Categorizes the email (Support, Sales, Complaint, Bug Report, etc.), assesses urgency (Critical/High/Normal/Low), detects sentiment, and flags escalation needs
2. **Extractor**: Pulls structured information: sender intent, explicit questions, requested actions, deadlines, reference numbers, people mentioned, and technical details
3. **Drafter**: Composes a professional response that addresses all questions, references relevant details, matches the appropriate tone, and includes clear next steps

## Usage

1. Run the application
2. Select a language model
3. Paste an email or type 'sample' for a built-in example
4. Watch the supervisor delegate to each specialist in real time
5. Receive classification, extraction, and a draft response

## Example

Type `sample` to process a built-in email from a frustrated customer about damaged product delivery. The agents will:
- Classify it as **Complaint / Critical / Angry / Escalation Needed**
- Extract order number, quantities, deadline, requested actions
- Draft an empathetic response with resolution steps

## Worker Agents

| Agent | Role | Output |
|-------|------|--------|
| Classifier | Triage & prioritize | Category, urgency, sentiment, escalation flag |
| Extractor | Information mining | Intent, questions, actions, deadlines, references |
| Drafter | Response composition | Subject line + full email body ready to send |

## Configuration

- **Supervisor planning**: Chain-of-Thought for task coordination
- **Worker planning**: None (fast, focused single-task execution)
- **Streaming**: Real-time output with color-coded agent identification
- **Timeout**: 10 minutes per email processing run
